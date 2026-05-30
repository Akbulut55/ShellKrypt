# ShellKrypt Disclaimer

ShellKrypt is local-only encrypted vault software. It is designed to store sensitive records in local `.skvault` files controlled by the user.

## No Warranty

ShellKrypt is provided as-is, without warranty of any kind. The project owner and contributors do not guarantee that the software is error-free, secure against every attack, compatible with every device, or suitable for every use case.

## No Password Recovery

ShellKrypt intentionally has no password recovery service.

If you forget the master password while a vault is locked, ShellKrypt cannot recover the encrypted data. The project owner, contributors, and official builds cannot decrypt the vault without the correct master password or a valid encrypted backup and backup passphrase.

Create and verify backups before relying on a vault.

## Security Scope

ShellKrypt uses local encryption mechanisms such as Argon2id-derived unlock keys and AES-GCM encrypted payloads. These are implementation choices, not a guarantee that every build, platform, dependency, device, or user workflow is risk-free.

ShellKrypt has not received an external security audit. Do not treat it as certified, independently audited, or appropriate for regulated enterprise requirements unless you perform your own review.

## User Responsibility

Users are responsible for:

- remembering master passwords and backup passphrases
- protecting local devices from malware and unauthorized access
- storing backups safely
- deleting plaintext exports when no longer needed
- verifying official downloads before trusting them
- complying with laws, workplace policies, and platform rules that apply to their use

## Plaintext Exports

Plaintext JSON exports and activity report exports are decrypted reports. They are more sensitive than encrypted vault files and should be stored, transferred, and deleted carefully.

## Clipboard Limitations

Clipboard clearing is best-effort. Operating systems, clipboard managers, remote desktop tools, and other applications may retain copied values outside ShellKrypt's control.

## Payment Card And Regulated Data

ShellKrypt can store card-like data if the user enters it, but ShellKrypt is not a PCI-certified payment-card system and is not a substitute for regulated data-handling infrastructure.

## Not Professional Advice

ShellKrypt documentation and security notes are product documentation, not a regulated compliance or professional security service.
