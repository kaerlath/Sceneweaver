# Upstream code and licenses

This project is distributed under AGPL-3.0-or-later; see LICENSE.md.

- `vendor/Intoner.Contracts/Contracts/*.cs` and `ObjectApiVersions.cs` are unmodified files from [Abelfreyja/Intoner.Api](https://github.com/Abelfreyja/Intoner.Api), revision `65930f89436faa5167e74d167a94e0bb745f9f00`, copyright its contributors, AGPL-3.0-or-later. The local csproj targets .NET 10 and includes only the DTO dependency, MessagePack.Annotations 3.1.7. This is a file-contract assembly named `Intoner.Contracts`, not an IPC client.
- `vendor/Stagehand.Definitions` C# files are unmodified from [universalconquistador/Stagehand](https://github.com/universalconquistador/Stagehand), revision `0e126ec52373971809725d807b9f988a97f40619`, copyright UniversalConquistador and contributors, AGPL-3.0-or-later. The local csproj removes packaging-only settings and the parent version import.
- Intoner implementation inspected at revision `ea7927276a6ffb8a7c335d122acef5640db0cde9`. Implementation source is not copied into the editor.
- `vendor/Stagehand.Catalog/paths.json` is the unmodified `Stagehand/Properties/paths.json` from [universalconquistador/Stagehand](https://github.com/universalconquistador/Stagehand), revision `0e126ec52373971809725d807b9f988a97f40619`, copyright UniversalConquistador and contributors, AGPL-3.0-or-later. Its license is included alongside it. The catalog contains paths only; models, textures and audio come from the user's game installation and are not redistributed.
- Dalamud, Lumina and ImGui bindings are referenced from the user's Dalamud installation and are not included in the plugin bundle. MessagePack.Annotations is restored via NuGet; its NuGet package supplies its licensing metadata.

When distributing a binary, provide the corresponding complete source, including these contract files and build instructions, under the applicable AGPL terms. No game assets are bundled in the plugin.
