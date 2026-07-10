# K3 Recovery Key Architecture

## Current State

Current `.k3enc` files derive AES/HMAC keys directly from the login password and `crypto_salt`:

```text
PBKDF2(password, crypto_salt, 100000) -> AES key + HMAC key
```

Because file keys are derived from the password, forgetting the password means old files cannot be decrypted by a new recovery key unless the file format or key model changes.

## Recommended V2 Design

Use a random vault master key.

```text
vault_master_key = random 64 bytes
password_key     = PBKDF2(password, salt, 100000, 64)
recovery_key     = random human-readable secret
recovery_wrap    = PBKDF2(recovery_key, recovery_salt, 100000, 64)
```

Store encrypted wrappers in `.vault_config.json`:

```json
{
  "crypto_version": 2,
  "master_key_wrapped_by_password": "...",
  "master_key_wrapped_by_recovery": "...",
  "recovery_key_status": "created",
  "recovery_salt": "..."
}
```

The user password and recovery key do not encrypt files directly. They only unwrap `vault_master_key`. Files are then encrypted with keys derived from `vault_master_key`.

## Recovery Flow

1. User selects "Recover vault".
2. App asks for recovery key.
3. App unwraps `vault_master_key`.
4. User sets a new password.
5. App writes a new password wrapper for the same `vault_master_key`.
6. Existing files remain decryptable.

## Compatibility Plan

V1 files remain supported forever:

```text
V1 .k3enc = [IV][HMAC][payload] using password-derived keys
```

V2 can use one of two paths:

1. Keep `.k3enc` container but derive per-file AES/HMAC keys from `vault_master_key`.
2. Add a new magic header such as `K3V2` for cleaner detection.

Recommendation: use `K3V2` for new files and keep the V1 decoder for old files. Add a migration wizard that decrypts V1 with the old password and re-encrypts to V2.

## UX Rules

- Show recovery key only once during setup.
- Require the user to confirm it was saved offline.
- Never write the plain recovery key to history, logs, reports, or screenshots.
- If recovery key is lost and password is forgotten, recovery is impossible.
- Self-destruct warnings must explicitly say whether recovery remains possible.

## Implementation Phases

1. Add config schema for `crypto_version`, wrapped master keys, and recovery status.
2. Add setup UI that generates and displays recovery key once.
3. Add V2 crypto engine while keeping V1 read support.
4. Add recovery flow to reset password.
5. Add migration wizard for existing V1 vaults.
6. Add Windows implementation and cross-platform test vectors before enabling by default.
