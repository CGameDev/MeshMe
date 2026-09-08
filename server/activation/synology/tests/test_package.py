import hashlib
import importlib.util
import io
import json
from pathlib import Path
import re
import tarfile
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("spk", ROOT / "build_package.py")
spk = importlib.util.module_from_spec(spec)
spec.loader.exec_module(spk)


class PackageTests(unittest.TestCase):
    def test_identity_and_no_automatic_publication(self):
        privilege = json.loads((ROOT / "conf/privilege").read_text())
        self.assertEqual(privilege["defaults"]["run-as"], "package")
        self.assertEqual(privilege["username"], "MeshMeActivation")
        self.assertNotIn("ctrl-script", privilege)
        resource = spk.resource()["webservice"]
        self.assertEqual(resource["portals"], [])
        for service in resource["services"]:
            self.assertEqual(service["php"]["user"], "MeshMeActivation")
            self.assertEqual(service["php"]["backend"], 11)
            self.assertIn("posix", service["php"]["extensions"])
        self.assertFalse(resource["services"][1]["support_alias"])

    def test_public_roots_contain_only_entry_points(self):
        files = spk.payload()
        self.assertEqual({n for n in files if n.startswith("web/")}, {
            "web/meshme-activation-api/index.php",
            "web/meshme-activation-api/.htaccess",
            "web/meshme-activation-admin/index.php",
            "web/meshme-activation-admin/.htaccess",
            "web/meshme-activation-admin/portal.css",
        })
        for suffix in ("api", "admin"):
            wrapper = files[f"web/meshme-activation-{suffix}/index.php"][0].decode()
            self.assertLess(wrapper.index("synology/bootstrap.php"), wrapper.index("/service/"))
        self.assertIn(b"LimitRequestBody 24576", files["web/meshme-activation-api/.htaccess"][0])
        self.assertIn(b"LimitRequestBody 8192", files["web/meshme-activation-admin/.htaccess"][0])

    def test_application_source_is_preserved(self):
        for name, (content, _) in spk.payload().items():
            if name.startswith("service/") and name.endswith((".php", ".sql", ".css")):
                self.assertEqual(content, (spk.SERVICE / name[8:]).read_bytes())

    def test_no_deployed_secrets_or_mutating_lifecycle(self):
        for name, (content, _) in spk.payload().items():
            self.assertFalse(name.endswith((".pem", "/config.php", ".spk")))
            self.assertNotIn(b"-----BEGIN PRIVATE KEY-----", content)
            self.assertNotIn(b"-----BEGIN RSA PRIVATE KEY-----", content)
        script = (ROOT / "scripts/postinst").read_text()
        self.assertNotIn("/volume1/", script)
        self.assertNotIn("synoacltool", script)
        self.assertNotRegex(script, r"(?m)^\s*(rm|chown|mysql|mariadb|openssl|curl|wget)\b")

    def test_archive_checksum_modes_and_reproducibility(self):
        with tempfile.TemporaryDirectory() as tmp:
            first = spk.build(Path(tmp) / "first.spk")
            self.assertEqual(first, spk.build(Path(tmp) / "second.spk"))
            with tarfile.open(fileobj=io.BytesIO(first)) as outer:
                info = outer.extractfile("INFO").read().decode()
                payload = outer.extractfile("package.tgz").read()
                self.assertIn(f'checksum="{hashlib.md5(payload).hexdigest()}"', info)
                self.assertIn('arch="noarch"', info)
                for member in outer.getmembers():
                    self.assertFalse(member.name.startswith("/"))
                    self.assertNotIn("..", Path(member.name).parts)
                    if member.name.startswith("scripts/"):
                        self.assertEqual(member.mode, 0o755)
                        self.assertNotIn(b"\r", outer.extractfile(member).read())
                with tarfile.open(fileobj=io.BytesIO(payload), mode="r:gz") as inner:
                    self.assertEqual(set(inner.getnames()), set(spk.payload()))


if __name__ == "__main__":
    unittest.main()
