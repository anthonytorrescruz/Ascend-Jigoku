# Release and browser play

Publish only after teammate integration, asset permission review and final acceptance testing. These instructions are preparation; no release or website is currently claimed to be live.

## Windows build for grading

1. Use Unity 6000.5.9f1 with Windows Build Support. Open the final integrated project and resolve compile/import errors.
2. Confirm MainMenu is the first enabled scene in Build Profiles and START points to ProceduralPillar. Include every scene loaded at runtime, including Death. Retain development scenes in source; include them in the release only if intentionally needed.
3. Build a normal Windows x86_64 player into a clean output folder. Confirm release/death-screen settings and desired debug visibility.
4. Test the **built** application, then ZIP the entire folder including the executable, `_Data`, UnityPlayer.dll and all other build dependencies. Do not distribute the executable alone.
5. After all source/docs are merged to GitHub, create a release/tag for that exact commit and attach the ZIP. Include supported OS, controls, launch steps, known limitations and actual validation performed.
6. Link that release from README and the submission checklist. GitHub's automatically generated source ZIP is not this player build.

GitHub Releases supports downloadable software packages: https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases

## Playing in a browser through GitHub Pages

Yes: Unity can build the game for Web, and GitHub Pages can host the resulting static HTML/JavaScript/WebAssembly files. The normal repository page cannot execute a Unity project or Windows `.exe`.

WebGL Build Support is already installed alongside this project's Unity editor. A Web build still needs its own compatibility verification, including saving, audio startup, controller support, performance and display sizing.

1. Add/select a Web build profile with MainMenu first and the required runtime scenes.
2. For a simple static-host deployment, use uncompressed build files or an appropriate Unity decompression fallback; do not assume GitHub Pages supplies Unity-specific compression headers. Avoid requiring shared-memory/thread headers unless the hosting setup supports them.
3. Produce the complete Web output (index.html plus Build/TemplateData and related files). Serve it over HTTP(S); opening the HTML directly from disk is not a valid deployment test.
4. Publish that static output through a dedicated Pages branch or a Pages Actions deployment. Keep the source project and documentation separate from the generated Web output. A branch-based site commonly uses `.nojekyll`.
5. Enable/configure Pages in the repository settings and verify the deployed build. The expected project-site URL would be `https://anthonytorrescruz.github.io/Ascend-Jigoku/` once deployed; this is **not a claim that the URL is live**.
6. Add a real PLAY IN BROWSER link to README only after the deployed game works.

References: [GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages), [Unity Web deployment](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html).

Given the course deadline, a verified Windows release is the primary delivery path. Treat Web play as an additional convenience until it has equivalent validation. Save-file behavior in particular must be checked on the browser platform rather than assumed from Windows.
