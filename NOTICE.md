# ShellKrypt: Notice

This document records notices for official ShellKrypt packages and for any
copying or distribution separately authorized by the copyright owner. It does
not itself grant permission to distribute ShellKrypt.

## Project Notice

- Project name: ShellKrypt.
- Copyright: Copyright (C) 2026 the ShellKrypt author, publishing as Karvulas.
- Public project identity: Karvulas.
- Legal status: Karvulas is the author's public project name, not an
  incorporated company or separate legal entity.
- Primary license or usage status: ShellKrypt Source License 1.0;
  source-available commercial pre-release software.
- Official source or home: [https://github.com/Akbulut55/ShellKrypt](https://github.com/Akbulut55/ShellKrypt).

## License Notice

> Current ShellKrypt source code is distributed under the ShellKrypt Source
> License 1.0. Personal noncommercial source builds and modifications are
> permitted, but redistribution and commercial use require separate written
> permission from the copyright owner.

License file or link:

- [`LICENSE`](LICENSE)

## Third-Party Notices

The following inventory covers direct runtime dependencies and bundled assets
in the current source tree. Versions are the versions declared by the project
files; release packaging must be re-audited against the final resolved
dependency graph.

| Material | Version | Upstream or copyright holder | Purpose | License | Distribution requirement |
|---|---:|---|---|---|---|
| Avalonia, Avalonia.Desktop, and Avalonia.Themes.Fluent | 12.1.0 | The AvaloniaUI Project | Desktop UI framework | MIT | Include the Avalonia MIT copyright and permission notice. |
| CommunityToolkit.Mvvm | 8.4.2 | .NET Foundation and contributors | Desktop MVVM infrastructure | MIT | Include its MIT license and package-provided third-party notice. |
| Konscious.Security.Cryptography.Argon2 | 1.3.1 | Keef Aragon | Argon2id derivation for vault and backup keys | MIT | Include the MIT copyright and permission notice. Its Blake2 dependency is also MIT. |
| Microsoft.Data.Sqlite | 10.0.3 | Microsoft | Managed SQLite access | MIT | Include the Microsoft MIT copyright and permission notice. |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.3 | SourceGear, LLC | SQLite native provider and bundle | Apache-2.0 | Include Apache-2.0 and preserve the `Copyright 2014-2026 SourceGear, LLC` notice. |
| SourceGear.sqlite3 package / SQLite native library | package 3.50.4.5; SQLite 3.50.4 | SourceGear, LLC and the SQLite authors | Native SQLite packaging and library | SQLite public domain dedication | Retain SourceGear's package ownership and copyright metadata alongside SQLite's public-domain statement. |
| SixLabors.ImageSharp | 3.1.12 | Six Labors and contributors | Loads QR-code images into pixels for Authenticator import | Apache-2.0 grant under the Six Labors Split License 1.0 | Reference only the granted Apache-2.0 license and include the Six Labors copyright notice. Reassess if the package version or ShellKrypt licensing model changes. |
| ZXing.Net | 0.16.10 | Michael Jahn and ZXing contributors | Decodes Authenticator QR codes | Apache-2.0 | Include Apache-2.0 and preserve applicable notices. |
| Inter | bundled variable fonts | The Inter Project Authors | Primary interface font | SIL Open Font License 1.1 | Keep the bundled `OFL.txt`; do not sell the font by itself or relicense it. Modified fonts must follow the reserved-name rules. |
| JetBrains Mono | bundled variable fonts | The JetBrains Mono Project Authors | Monospace values and code | SIL Open Font License 1.1 | Keep the bundled `OFL.txt`; do not sell the font by itself or relicense it. Modified fonts must follow the reserved-name rules. |
| Google Material Symbols-derived icon geometries | bundled in `Resources/Icons.axaml` | Google and Material Symbols contributors | Interface icons, excluding the first-party ShellKrypt logo | Apache-2.0 | Include Apache-2.0 and preserve applicable notices. Attribution in the UI is not required. |

The Apache-2.0 text applicable to the bundled icon geometries and the
Apache-licensed dependencies is included at
[`THIRD_PARTY_LICENSES/Apache-2.0.txt`](THIRD_PARTY_LICENSES/Apache-2.0.txt).

ImageSharp is a direct dependency. Version 3.1.12 grants use under Apache-2.0
when the consuming software is licensed under an open-source or
source-available license. ShellKrypt's current source-available model meets
that criterion, including if official builds are sold while that criterion
continues to be met. An ImageSharp upgrade or a future move away from a
qualifying source-available model requires a fresh license review; otherwise a
Six Labors commercial license may be required.

Avalonia's resolved Desktop dependency graph also distributes platform and
rendering components, including SkiaSharp 3.119.4, HarfBuzzSharp 8.3.1.3,
ANGLE native assets on Windows, Tmds.DBus.Protocol 0.94.1, and
MicroCom.Runtime 0.11.6. Their NuGet packages identify MIT or BSD-style terms
and, for the native graphics/text packages, provide additional
`THIRD-PARTY-NOTICES.txt` files. Official binary packages must reproduce those
package-supplied license and third-party notice files for the platforms they
actually contain.

The ShellKrypt logo and `main-logo.ico` are treated as first-party ShellKrypt
assets and are covered by the ShellKrypt Source License.

## Attribution

- Copyright and license notices from redistributed dependencies remain the
  property of their respective owners.
- Test-only dependencies are development inputs and are not part of the
  current runtime distribution inventory.
- A release must generate and verify a complete license bundle from the exact
  published artifacts; this source-tree inventory is not a substitute for
  inspecting self-contained runtime files, installers, or store packages.

## Official Distribution

- Official distribution locations: not finalized for the current pre-release stage.
- Official signing or verification: not finalized.
- Support or maintenance status: active development without a guaranteed support term.

Distribution rules:

- Verify an official source, signature, or checksum once official verification methods are published.
- Do not present third-party, modified, or unsigned builds as tested, signed, endorsed, or supported official ShellKrypt releases.
- Do not distribute source, compiled packages, installers, containers, or
  hosted ShellKrypt services without a separate written license.

## Modified Copies

- Personal noncommercial modifications are permitted by the active license.
- Distribution of modified source or binaries requires separate written
  permission from the copyright owner.
- Authorized modified distributions must preserve applicable ShellKrypt and
  third-party notices and must not imply official endorsement or support.

## Names, Logos, And Branding

- Use the ShellKrypt name honestly and in a way that does not confuse unofficial work with official releases.
- Do not use project logos, screenshots, domains, or branding to imply endorsement or support without permission.
- More specific trademark and branding rules may be published before official distribution.

## Open Notice Questions

- What official repository, domain, signing identity, and checksum channel will be published?
- Which branding uses will require explicit permission before public 1.0?
- What commercial license, pricing, support, and store terms will accompany
  official builds?
- Which .NET runtime packs and platform-native files will each final release
  artifact contain, and which package-provided notice files must therefore be
  included with that artifact?
