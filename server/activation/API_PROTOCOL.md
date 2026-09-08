# Activation protocol v1

Base: `https://consolecrate.xyz/meshme-activation/`. All API endpoints are under
`api/v1/`; admin endpoints are a separate private web root. Production HTTP and
redirect-following are forbidden. Requests are UTF-8 JSON, maximum 24 KiB.

POST common envelope:

```json
{
  "version": 1,
  "product": "meshme360",
  "appVersion": "0.1.1",
  "hardware": {
    "version": 1,
    "components": {
      "systemUuid": "64 lowercase SHA-256 hexadecimal characters",
      "baseboard": "64 lowercase SHA-256 hexadecimal characters"
    },
    "combined": "SHA-256 of sorted component-name:hash lines"
  }
}
```

The example values above describe shapes, not usable credentials or hashes.
Only these fields plus `key` and `token` are accepted. No project/model/texture data.
Optional key/token fields can be null. The native .NET host constructs requests;
the web app never receives keys or token payloads.

| Endpoint | Additional input | Result |
|---|---|---|
| POST trial/start | none; explicit user action | Original trial token for recognized PC, or new seven-day trial |
| POST activate | key | Paid entitlement or stable rejection |
| POST validate | token | Renewed paid lease/original unexpired trial after current DB status checks |
| POST deactivate | token | `{ "deactivated": true }`; repeated requests are harmless |
| GET health | none | `{ "status": "ok", "protocol": 1 }` |

Signed token envelope: `payload` (standard base64 exact UTF-8 JSON bytes), `signature`
(standard base64 RSA-3072 PKCS#1 v1.5 SHA-256 signature over those bytes), `keyId`.
The server must not reserialize a token before verification. Signed payload contains
`version`, `product`, `type` (trial/license), `id`, `hardware`, `issuedAt`, `expiresAt`,
`nextValidation`, `keyId`, `nonce`. Times are integer Unix seconds, UTC.
The client enforces product, version, key id, signature, time bounds, maximum trial/
lease duration and hardware binding. Server validation accepts expired *signed*
lease proofs to renew an otherwise active license; it always checks current DB status.

Keys use `0123456789ABCDEFGHJKMNPQRSTVWXYZ`. Normalize by removing whitespace/hyphens
and uppercasing. The first 24 symbols are independent secure random values (120 bits).
Checksum starts at zero: for each symbol index, `sum = (sum*17 + index) mod 32`.
The final symbol is alphabet[sum]. The checksum detects typing errors, not forgery.
The database stores SHA-256 of the normalized full key plus a masked hint only.

Fingerprint v1 hashes normalized components as UTF-8
`meshme360:hardware:v1:<componentName>:<normalizedValue>` using SHA-256. UUIDs are
uppercase canonical hyphenated UUID strings. Serials are trimmed and uppercased;
known OEM/empty/zero placeholders are omitted. Combined fingerprint hashes sorted
`name:componentHash` lines joined by LF, with no trailing LF. Matching uses weights
50/25/15/10 for systemUuid/baseboard/bios/machineGuid with threshold 60, plus a
strong-component conflict veto. No strong component or less than 60 available points
requires support; the implementation never fabricates identity or silently weakens policy.
Original server device anchors are retained to prevent gradual identity drift.

Requests for one product lock its product row first, then key rows. This deliberately
serializes device recognition/trials, normal activation and admin changes. It favors
correctness for a NAS-scale service over high parallel write throughput. Same-device
reactivation returns the existing activation id and never consumes another slot.

Stable error envelope: `{ "code": "...", "message": "safe text", "requestId": "..." }`.
Codes include INVALID_REQUEST, INVALID_KEY, KEY_REVOKED, KEY_SUSPENDED,
DEVICE_LIMIT_REACHED, TRIAL_EXPIRED, DEVICE_CHANGED, UPDATE_REQUIRED,
RATE_LIMITED, SERVICE_UNAVAILABLE. The client maps codes to local safe messages.
Correlation logs contain only ids/codes, never full keys, serials, bodies or paths.
IP/key/device dimensions allow 20 attempts/hour, followed by exponential blocking
capped at one hour. Apply a front-proxy request limit as a second layer.
