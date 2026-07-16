# ShellKrypt: Disclaimer

This document describes limits and risks for ShellKrypt. It should be reviewed
before public distribution, sale, or reliance on the software.

Related documents:

- Terms of use: [`TERMS.md`](TERMS.md)
- Privacy notice: [`PRIVACY.md`](PRIVACY.md)
- Security notes: [`SECURITY.md`](SECURITY.md)
- Notice: [`NOTICE.md`](NOTICE.md)

## General Disclaimer

> ShellKrypt is pre-release local encrypted-vault software provided for use at the user's own risk. Users remain responsible for passwords, devices, backups, exports, and decisions based on the software.

## No Warranty

The project does not guarantee:

- That ShellKrypt is error-free, secure against every attack, or suitable for regulated use.
- That every build, dependency, platform, file, or future update remains compatible or available.
- That forgotten master passwords, forgotten backup passphrases, deleted files, or damaged vaults can be recovered.

## Reliance And Use Risk

- Do not rely on ShellKrypt as the only copy of important data; create and verify independent encrypted backups.
- Security Audit, Backup Center, Emergency Kit, and status indicators are advisory and can miss risks.
- Pre-1.0 behavior, formats, and interfaces may change, and undocumented behavior is not a stable contract.

## Data, Export, And Output Risk

- Plaintext JSON exports and activity reports are decrypted output and can expose sensitive information outside the vault.
- Clipboard managers, remote desktop tools, malware, indexing, synchronization, and backup software can retain copied or exported data beyond ShellKrypt's control.
- Printable or metadata-only reports may still reveal names, paths, filenames, timestamps, counts, and security-readiness information.

## Security Limits

- ShellKrypt has not received an external security audit and is not certified for enterprise, payment-card, medical, legal, or compliance workloads.
- Argon2id and AES-GCM are implementation choices, not a guarantee against a compromised device, malicious build, weak password, dependency flaw, or misuse.
- Secrets can exist in process memory while a vault is unlocked, and clipboard clearing is best-effort rather than a security boundary.

## Availability, Support, And Compatibility Limits

- The project may change, break, pause, or be withdrawn without a guaranteed support period.
- Automatic backups run only under their documented in-app conditions and are not an operating-system backup service.
- Platform-specific packaging, code signing, and update delivery may be incomplete or unsupported.

## Not Professional Advice

> ShellKrypt, its documentation, audit findings, and security notes are not legal, financial, compliance, payment-card, or professional security advice. Obtain qualified advice for regulated or high-risk use.

## Open Disclaimer Questions

- Which jurisdiction-specific liability language is required before official paid distribution?
- What external review must occur before stronger security claims are permitted?
- Which support and refund terms will accompany official distribution channels?
