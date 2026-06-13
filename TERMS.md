# ShellKrypt: Terms Of Use

Status: ShellKrypt project terms for locally distributed desktop builds.

## 1. Acceptance

By installing, running, or using ShellKrypt, you acknowledge these terms, the privacy notice, the security policy, and the disclaimer included with the project. If you do not agree, do not use ShellKrypt for real data.

## 2. Product Scope

ShellKrypt is a local-only encrypted vault application. It is intended to store user-managed vault files, backups, exports, and app metadata on the user's own device or user-selected storage locations. ShellKrypt does not provide a cloud account, hosted sync service, remote recovery service, or server-side access to user vaults by default.

ShellKrypt is not externally audited yet. Security claims must remain limited to the actual design and implementation described in `README.md`, `SECURITY.md`, and `DISCLAIMER.md`.

## 3. No Password Recovery

ShellKrypt cannot recover a forgotten master password. If a vault is locked and the master password is lost, the encrypted data cannot be recovered by ShellKrypt, the project owner, or anyone else unless the user has a valid backup and the required backup passphrase.

Users are responsible for creating, protecting, and verifying backups. Emergency Kit and automatic-backup features are readiness aids only; they do not create a password recovery service.

## 4. User Responsibilities

Users are responsible for:

- choosing and remembering strong master passwords and backup passphrases
- keeping backups, exports, and vault files secure
- protecting the device, operating system account, clipboard, and filesystem
- deleting plaintext exports when they are no longer needed
- understanding that in-app automatic backups run only while ShellKrypt is open, the vault is unlocked, and the session backup passphrase is available
- ensuring that their use of ShellKrypt complies with applicable laws, workplace rules, and data-handling obligations

ShellKrypt is not a PCI-certified product, medical-record system, legal record system, or regulated compliance platform.

## 5. Plaintext Exports And Clipboard

Plaintext exports and activity report exports are decrypted reports. They may expose sensitive data if stored, shared, synced, backed up, indexed, or uploaded outside ShellKrypt.

Clipboard clearing is best-effort and is not a security boundary. Other applications, the operating system, clipboard managers, remote desktop tools, and malware may observe clipboard contents.

## 6. License And Official Builds

ShellKrypt source code is prepared for release under `GPL-3.0-or-later`. The full license text is in `LICENSE`.

Official signed builds, paid distribution channels, support services, names, logos, and release infrastructure may be offered separately from the source license. Modified builds must not misrepresent themselves as official ShellKrypt releases. See `NOTICE.md`.

## 7. Updates And Support

Pre-release builds may change, break, or be withdrawn. Support channels, update schedules, refund rules, and commercial distribution terms are controlled by the distribution channel or separate written policy when one is provided.

Do not rely on undocumented behavior as a stable contract before a public 1.0 release.

## 8. No Warranty

ShellKrypt is provided as-is, without warranties of any kind. The project owner does not guarantee that ShellKrypt is error-free, secure against all threats, compatible with every device, or suitable for every use case.

To the maximum extent permitted by applicable law, the project owner is not liable for lost data, lost passwords, exposed exports, device compromise, business interruption, or other damages arising from use or inability to use ShellKrypt.

## 9. Changes

These terms may change for future builds. Material changes should be reflected in `CHANGELOG.md` or release notes.
