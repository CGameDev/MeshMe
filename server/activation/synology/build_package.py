"""Build a reproducible, source-only DSM 7.3 SPK; never read deployed secrets."""
from __future__ import annotations

import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import tarfile

HERE = Path(__file__).resolve().parent
SERVICE = HERE.parent
REPO = SERVICE.parents[1]
PACKAGE = "MeshMeActivation"
VERSION = "0.1.0-0001"
HOME = f"/var/packages/{PACKAGE}/target"
VAR = f"/var/packages/{PACKAGE}/var"


def archive(entries: dict[str, tuple[bytes, int]], compressed: bool = False) -> bytes:
    raw = io.BytesIO()
    with tarfile.open(fileobj=raw, mode="w", format=tarfile.GNU_FORMAT) as tar:
        for name, (data, mode) in sorted(entries.items()):
            info = tarfile.TarInfo(name)
            info.size, info.mode, info.mtime = len(data), mode, 0
            info.uid = info.gid = 0
            info.uname = info.gname = "root"
            tar.addfile(info, io.BytesIO(data))
    return gzip.compress(raw.getvalue(), mtime=0) if compressed else raw.getvalue()


def resource() -> dict:
    services, directories = [], []
    for suffix, display in [("api", "MeshMe Activation API (Dedicated)"),
                            ("admin", "MeshMe Activation Admin (Private)")]:
        webroot = f"meshme-activation-{suffix}"
        services.append({
            "service": f"meshme-activation-{suffix}", "display_name": display,
            "support_alias": suffix == "api", "support_server": True,
            "type": "apache_php", "root": webroot, "backend": 2,
            "intercept_errors": False,
            "php": {
                "profile_name": f"MeshMeActivation-{suffix}",
                "profile_desc": f"PHP 8.2 dedicated activation account ({suffix})",
                # PHP 8.2 = 11: verified against SynoCommunity demowebservice
                # spk/demowebservice/src/conf_php8.2/resource (2026-09-08).
                "backend": 11, "user": PACKAGE, "group": "http",
                "extensions": ["openssl", "pdo_mysql", "posix"],
                "open_basedir": ":".join([
                    f"/var/services/web_packages/{webroot}", HOME, VAR,
                    "/volume1/meshme-private", "/run/mysqld"]),
                "php_settings": {
                    "display_errors": "0", "display_startup_errors": "0",
                    "log_errors": "1", "expose_php": "0",
                    "error_log": f"{VAR}/php-errors.log",
                    "session.save_path": f"{VAR}/sessions",
                    "session.cookie_secure": "1", "session.cookie_httponly": "1",
                    "session.cookie_samesite": "Strict", "session.use_strict_mode": "1",
                    "memory_limit": "128M", "max_execution_time": "30",
                    "post_max_size": "24K", "file_uploads": "0"
                }
            }, "connect_timeout": 20, "read_timeout": 35, "send_timeout": 35
        })
        directories.append({
            "source": f"{HOME}/web/{webroot}", "target": webroot,
            "mode": "0750", "user": PACKAGE, "group": "http"
        })
    # Deliberately no default portals: neither replace the existing public alias
    # nor expose the administrator site during package installation.
    return {"webservice": {"services": services, "portals": [],
                           "pkg_dir_prepare": directories}}


def payload() -> dict[str, tuple[bytes, int]]:
    entries = {}
    # Allowlisted source directories; config.php, PEMs, database dumps and local
    # test outputs are never discovered or read by this builder.
    for directory in ("src", "public", "admin-public", "migrations", "bin"):
        for path in sorted((SERVICE / directory).rglob("*")):
            if path.is_file() and path.suffix in (".php", ".sql", ".css"):
                entries["service/" + path.relative_to(SERVICE).as_posix()] = (path.read_bytes(), 0o644)
    for name in ("API_PROTOCOL.md", "SECURITY_NOTES.md", "README_DEPLOY_SYNOLOGY.md"):
        entries["service/" + name] = ((SERVICE / name).read_bytes(), 0o644)
    entries["service/config/config.example.php"] = ((SERVICE / "config/config.example.php").read_bytes(), 0o644)
    entries["synology/bootstrap.php"] = ((HERE / "bootstrap.php").read_bytes(), 0o644)
    entries["README_SYNOLOGY_PACKAGE.md"] = ((HERE / "README.md").read_bytes(), 0o644)
    entries["LICENSE"] = ((REPO / "LICENSE").read_bytes(), 0o644)
    for suffix, source in (("api", "public"), ("admin", "admin-public")):
        prefix = f"web/meshme-activation-{suffix}/"
        entry = ("<?php\ndeclare(strict_types=1);\n"
                 f"require '{HOME}/synology/bootstrap.php';\n"
                 f"require '{HOME}/service/{source}/index.php';\n")
        entries[prefix + "index.php"] = (entry.encode(), 0o644)
        limit = 24576 if suffix == "api" else 8192
        htaccess = f"Options -Indexes\nLimitRequestBody {limit}\n"
        if suffix == "api":
            htaccess += "RewriteEngine On\nRewriteCond %{REQUEST_FILENAME} !-f\nRewriteRule ^ index.php [END]\n"
        entries[prefix + ".htaccess"] = (htaccess.encode(), 0o644)
        if suffix == "admin":
            entries[prefix + "portal.css"] = ((SERVICE / "admin-public/portal.css").read_bytes(), 0o644)
    return entries


def build(destination: Path) -> bytes:
    packed = archive(payload(), compressed=True)
    fields = {
        "package": PACKAGE, "displayname": "Mesh Me360 Activation Service",
        "version": VERSION, "arch": "noarch", "os_min_ver": "7.3-86009",
        "maintainer": "Computer Universal Technology Systems",
        "description": "Dedicated-account PHP 8.2 activation API and private administrator service. Manual configuration and owner acceptance required.",
        "install_dep_packages": "WebStation:PHP8.2:Apache2.4:MariaDB10",
        "start_dep_packages": "WebStation:PHP8.2:Apache2.4:MariaDB10",
        "thirdparty": "yes", "maintainer_url": "https://github.com/CGameDev/MeshMe",
        "distributor": "Computer Universal Technology Systems",
        "distributor_url": "https://github.com/CGameDev/MeshMe",
        "checksum": hashlib.md5(packed).hexdigest(),
        "extractsize": str((sum(len(v[0]) for v in payload().values()) + 1023) // 1024)
    }
    entries = {
        "INFO": (("\n".join(f'{k}="{v}"' for k, v in fields.items()) + "\n").encode(), 0o644),
        "package.tgz": (packed, 0o644),
        "conf/privilege": ((HERE / "conf/privilege").read_bytes(), 0o644),
        "conf/resource": ((json.dumps(resource(), indent=2) + "\n").encode(), 0o644),
        "LICENSE": ((REPO / "LICENSE").read_bytes(), 0o644)
    }
    for name in ("preinst", "preuninst", "postuninst", "preupgrade"):
        entries["scripts/" + name] = (b"#!/bin/sh\nexit 0\n", 0o755)
    for name in ("postinst", "start-stop-status"):
        entries["scripts/" + name] = ((HERE / "scripts" / name).read_bytes().replace(b"\r\n", b"\n"), 0o755)
    entries["scripts/postupgrade"] = entries["scripts/postinst"]
    result = archive(entries)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(result)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    result = build(args.output)
    print(f"Built {args.output.name}: {len(result)} bytes; SHA256 {hashlib.sha256(result).hexdigest()}")
