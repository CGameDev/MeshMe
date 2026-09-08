"""PHP tests use simulated account lookups; actual NAS identity is a separate gate."""
import os
from pathlib import Path
import shutil
import subprocess
import unittest

PHP = os.environ.get("MESHME_TEST_PHP") or shutil.which("php")
BOOTSTRAP = Path(__file__).resolve().parents[1] / "bootstrap.php"


@unittest.skipUnless(PHP, "Set MESHME_TEST_PHP to PHP 8.2 for runtime guard tests")
class RuntimeGuardTests(unittest.TestCase):
    def run_guard(self, functions):
        code = functions + "require " + repr(BOOTSTRAP.as_posix()) + ";"
        code += "echo getenv('MESHME_ACTIVATION_HOME').'|'.getenv('MESHME_ACTIVATION_CONFIG');"
        return subprocess.run([PHP, "-d", "disable_functions=posix_geteuid,posix_getpwuid", "-r", code],
                              check=True, capture_output=True, text=True).stdout

    def test_shared_account_is_rejected(self):
        out = self.run_guard("function posix_geteuid(){return 1023;} function posix_getpwuid($id){return ['name'=>'http'];}")
        self.assertIn('"code":"SERVICE_UNAVAILABLE"', out)
        self.assertNotIn("/volume1/", out)

    def test_missing_posix_is_rejected(self):
        self.assertIn('"code":"SERVICE_UNAVAILABLE"', self.run_guard(""))

    def test_dedicated_account_uses_only_private_paths(self):
        out = self.run_guard("function posix_geteuid(){return 1234;} function posix_getpwuid($id){return ['name'=>'MeshMeActivation'];}")
        self.assertEqual(out, "/var/packages/MeshMeActivation/target/service|/volume1/meshme-private/config.php")


if __name__ == "__main__":
    unittest.main()
