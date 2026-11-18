# Submission readiness review

Reviewed November 18, 2026 against the supplied Software Engineering Project Rubric, Team Project assignment, and professor's November 16 reminder. This is an evidence-based readiness review, not a promised grade or a report of a new playthrough.

## Course requirements

- The assignment permits an application idea/type of the team's choice; a Unity game fits that scope.
- It requires teamwork and software engineering techniques, GitHub submission, an application demonstration, and each member identifying their contributions.
- The email requires the **GitHub repository link by Friday November 20, 2026 at 11:59 PM**, with no alternative submission format.
- The **complete application walkthrough video with sound is due Sunday November 22 at 11:59 PM**; one video submission per group is requested.
- The D2L contribution questionnaire is due by the final exam. Confirm the exam date and submission timezone in D2L.
- The professor refers to an example submission linked in D2L. That example was not supplied or reviewed here; compare the final repository against it.

## Coding rubric

The provided rubric totals **50 points**, representing **50% of the course grade**.

| Criterion | Points | Current evidence | Remaining grade risk / next action |
| --- | ---: | --- | --- |
| Functional correctness | 15 | Movement, combat, two bosses, shop/upgrades, menus, save/continue, tutorial and ending exist in source; historical controlled combat checks are included | Integrate teammate work and verify the final standalone game from START through checkpoint-two ending. No current release-wide pass is claimed |
| Code structure and organization | 9 | Separate component folders, state machines, central resolver, health components, generation/persistence/UI separation | Keep experimental scenes distinct; explain responsibilities using ARCHITECTURE.md |
| Code quality and readability | 7 | Meaningful system/class names and contextual comments | Some boss/encounter methods use dense one-line formatting and abbreviated locals. A focused readability pass remains worthwhile |
| Error handling and edge cases | 4 | Damage guards, capped upgrades, purchase validation, save parsing/recovery, route locks and ending safety | Validate corrupted saves, interrupted ending, zero-health flow, controller disconnection, scene transitions and invalid purchases in the integrated build |
| Efficiency and performance | 3 | Streamed chunks/enemies, reused query lists and bounded boss hazards | Record target hardware and actual frame-rate observations. No performance measurements were made in this review |
| Security and safe coding | 4 | Local-only design; no account/server secrets required; input/purchase/save checks | Recheck final history/assets for credentials and unsupported data. Basic string scanning is not a comprehensive security audit |
| Maintainability and extensibility | 3 | ScriptableObject tuning and reusable encounter/combat components | Avoid duplicated edits between test scenes and production prefabs; keep tuning documented |
| Documentation | 5 | README, controls, architecture/tuning, team, release guide, test checklist, demo plan and credits review prepared | Add real final-build/demo links, screenshots, verified contribution evidence and acceptance results after integration |

Visual polish helps demonstrate usability, but the rubric does not award a separate graphics/art category. Functionality, organization/readability and documentation should take priority over new cosmetic scope.

## Repository snapshot and release blockers

At review time, public GitHub main was `87cbdeeafdaa7cdc52b0f580119a3c352d9323a5` (November 16 mirror of UVCS changeset 125). The local repository had reached changeset 130 before these documentation additions. GitHub therefore did not yet contain all current fixes. Its root README was minimal and its release list was empty.

- [ ] Finish integrating teammates' work while retaining current boss, checkpoint and ending behavior.
- [ ] Clear merge conflicts and compile errors; have someone open a clean checkout.
- [ ] Review raw third-party asset redistribution and missing permissions in THIRD_PARTY.md.
- [ ] Complete the release acceptance checklist in TESTING.md and record actual results.
- [ ] Publish an extracted-and-tested Windows ZIP in GitHub Releases, then link it from README.
- [ ] Add 2-3 genuine gameplay screenshots and the final walkthrough link. Do not substitute concept art for application evidence.
- [ ] Confirm each member's contributions and attach actual sprint evidence.
- [ ] Check all README links and the professor's access to the repository/release/video.
- [ ] Submit the repository URL in D2L by November 20; submit the narrated, audible video by November 22.

No gameplay tests, new build, GitHub upload, permission change, or teammate merge was performed during this documentation review. The earlier request to leave playtesting to the team remains in effect.
