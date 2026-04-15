---
name: Always update FEATURE_TEST_CHECKLIST.md when adding features
description: CRITICAL — every new feature must be added to the feature test checklist immediately so user can confirm and test
type: feedback
---

Always update `docs/FEATURE_TEST_CHECKLIST.md` when building new features. The user uses this as the definitive testing reference.

**Why:** User explicitly requested this on 2026-03-28. The checklist is used to confirm and test every feature. If a feature isn't in the checklist, it can't be verified.

**How to apply:**
1. After building any new feature, panel, API endpoint, or behavior — immediately add test items to the checklist
2. Each item should be a manually testable checkbox (`- [ ] description`)
3. Group items under numbered sections (## N. Feature Name)
4. Include both happy path and edge cases
5. Update the "Last updated" date at the bottom
6. Update the total count in CLAUDE.md if mentioned there
