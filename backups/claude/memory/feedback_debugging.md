---
name: Debugging approach
description: When something works in one panel but not another, diff the working vs broken implementation first before guessing fixes
type: feedback
---

When a feature works in one grid/panel but not another, immediately compare the two implementations line-by-line (XAML + code-behind) before trying fixes.

**Why:** Wasted 5+ rebuild cycles on B2B editing because I tried clearing layouts, forcing AllowEditing, etc. instead of comparing to P2P's working XAML — the fix was `NavigationStyle="Cell"` vs `"Row"`, visible in the first line of the TableView.

**How to apply:**
- Diff the working panel's XAML and code-behind against the broken one first
- Query actual DB data before writing filters (e.g. building values differed between tables)
- Check DX API availability (grep DLLs or minimal build) before writing full implementations
- Add one debug line to confirm the theory, then fix once — don't stack speculative fixes
