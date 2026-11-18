# Releases and browser play

## Current Windows release

**[Download Ascend Jigoku v1.0.0 for 64-bit Windows](https://github.com/anthonytorrescruz/Ascend-Jigoku/releases/tag/v1.0.0).**

Download `Ascend-Jigoku-Windows-v1.0.0.zip`, extract the entire ZIP, and run `Ascend Jigoku.exe`. Unity is not required. Keep the executable, `_Data` folder, DLLs and all other included dependencies together. GitHub's automatic source archives are not the playable build. macOS and Linux require separate builds.

The game was built with Unity 6000.5.9f1 from UVCS changeset 133, Git commit `96d9a97bb1771a25239373a03b29a8a30d1641ba`, using an isolated copy of the published source. Packaging did not change gameplay. `RELEASE-INFO.txt` in the ZIP records the actual validation performed; full acceptance testing remains the team's responsibility.

The release includes a SHA-256 checksum. On Windows, `Get-FileHash -Algorithm SHA256` can verify the downloaded ZIP against `SHA256SUMS.txt`.

## Project website

**[Open the project page](https://anthonytorrescruz.github.io/Ascend-Jigoku/).** GitHub Pages hosts documentation and the download link. It does not run the game in the browser. Pages is published from the separate `gh-pages` branch.

## Creating another Windows release

1. Finish integration and select the exact source commit to release. Use Unity 6000.5.9f1 with Windows Build Support.
2. Build a normal Windows x86_64 player into a clean folder. Keep MainMenu first, with ProceduralPillar and Death available. Preserve the source project's scene and gameplay settings.
3. Check the built application and record the actual results. Complete the acceptance checklist in TESTING.md before claiming full validation.
4. Include instructions, controls, credits, version/source information and all build dependencies. ZIP the whole player folder, and calculate its SHA-256 checksum.
5. Create a GitHub release/tag for the source commit, attach the ZIP and checksum, and publish accurate notes including known limitations.
6. Update the README and Pages download links. Keep source/doc changes synchronized with the UVCS-to-GitHub mirror.

## Optional browser build

Browser play needs a separate Unity Web build; uploading Unity source or a Windows executable to Pages does not make it playable there. Validate saving, audio startup, controller input, performance and display scaling on Web before publishing it.

For Pages, use uncompressed output or a supported decompression fallback rather than assuming Unity-specific compression headers are available. Serve the complete generated HTML/JavaScript/WebAssembly output over HTTPS and verify the deployed game before adding a PLAY IN BROWSER link.

References: [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases), [GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages), [Unity Windows builds](https://docs.unity3d.com/6000.0/Documentation/Manual/WindowsStandaloneBinaries.html), [Unity Web deployment](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html).
