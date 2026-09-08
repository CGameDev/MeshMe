-- Mesh Me360 activation v1. All *_utc values are Unix seconds in authoritative server UTC.
CREATE TABLE products (
 id INT PRIMARY KEY AUTO_INCREMENT, code VARCHAR(32) NOT NULL UNIQUE, name VARCHAR(100) NOT NULL,
 active BOOLEAN NOT NULL DEFAULT 1, validation_days INT NOT NULL DEFAULT 7, grace_days INT NOT NULL DEFAULT 30,
 created_utc BIGINT NOT NULL, updated_utc BIGINT NOT NULL
) ENGINE=InnoDB;
INSERT INTO products(code,name,created_utc,updated_utc) VALUES('meshme360','Mesh Me360',UNIX_TIMESTAMP(),UNIX_TIMESTAMP());
CREATE TABLE license_keys (
 id CHAR(32) PRIMARY KEY, product_id INT NOT NULL, key_hash CHAR(64) NOT NULL UNIQUE, key_hint VARCHAR(20) NOT NULL,
 status ENUM('active','suspended','revoked') NOT NULL DEFAULT 'active', max_devices INT NOT NULL DEFAULT 1,
 reference VARCHAR(200) NOT NULL DEFAULT '', expires_utc BIGINT NULL, created_utc BIGINT NOT NULL, updated_utc BIGINT NOT NULL,
 FOREIGN KEY(product_id) REFERENCES products(id), CHECK(max_devices BETWEEN 1 AND 100)
) ENGINE=InnoDB;
CREATE TABLE devices (
 id CHAR(32) PRIMARY KEY, fingerprint_version INT NOT NULL, combined CHAR(64) NOT NULL,
 system_uuid CHAR(64) NULL, baseboard CHAR(64) NULL, bios CHAR(64) NULL, machine_guid CHAR(64) NULL,
 components_json TEXT NOT NULL, first_seen_utc BIGINT NOT NULL, last_seen_utc BIGINT NOT NULL,
 INDEX(system_uuid), INDEX(baseboard), INDEX(combined)
) ENGINE=InnoDB;
CREATE TABLE activations (
 id CHAR(32) PRIMARY KEY, license_key_id CHAR(32) NOT NULL, device_id CHAR(32) NOT NULL,
 status ENUM('active','deactivated','revoked') NOT NULL DEFAULT 'active',
 activated_utc BIGINT NOT NULL, last_validated_utc BIGINT NOT NULL, deactivated_utc BIGINT NULL,
 app_version VARCHAR(40) NOT NULL, policy_version INT NOT NULL DEFAULT 1,
 FOREIGN KEY(license_key_id) REFERENCES license_keys(id), FOREIGN KEY(device_id) REFERENCES devices(id),
 UNIQUE(license_key_id,device_id), INDEX(license_key_id,status)
) ENGINE=InnoDB;
CREATE TABLE trials (
 id CHAR(32) PRIMARY KEY, product_id INT NOT NULL, device_id CHAR(32) NOT NULL,
 started_utc BIGINT NOT NULL, expires_utc BIGINT NOT NULL, last_validated_utc BIGINT NOT NULL,
 status ENUM('active','revoked') NOT NULL DEFAULT 'active',
 FOREIGN KEY(product_id) REFERENCES products(id), FOREIGN KEY(device_id) REFERENCES devices(id), UNIQUE(product_id,device_id)
) ENGINE=InnoDB;
CREATE TABLE activation_events (
 id BIGINT PRIMARY KEY AUTO_INCREMENT, occurred_utc BIGINT NOT NULL, event VARCHAR(64) NOT NULL,
 subject_id VARCHAR(64) NULL, actor VARCHAR(80) NOT NULL, request_id CHAR(32) NOT NULL,
 INDEX(occurred_utc), INDEX(subject_id)
) ENGINE=InnoDB;
CREATE TABLE rate_limits (
 dimension CHAR(64) PRIMARY KEY, attempts INT NOT NULL, window_utc BIGINT NOT NULL, blocked_until_utc BIGINT NOT NULL
) ENGINE=InnoDB;
CREATE TABLE admins (
 id CHAR(32) PRIMARY KEY, username VARCHAR(80) NOT NULL UNIQUE, password_hash VARCHAR(255) NOT NULL,
 last_login_utc BIGINT NULL
) ENGINE=InnoDB;
