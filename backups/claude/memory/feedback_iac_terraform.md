---
name: All infrastructure as Terraform + Terragrunt
description: User requires all infrastructure to be IaC with Terraform modules and Terragrunt for multi-environment management
type: feedback
---

All infrastructure must be Terraform + Terragrunt. No ad-hoc scripts for infra provisioning.

**Why:** User explicitly requested "all infra should be infra as code terraform with terragrunt" (2026-03-30).

**How to apply:**
- New infrastructure goes in `infra/modules/` as a Terraform module
- Environment configs in `infra/environments/{local,dev,staging,prod}/`
- DRY configs in `infra/environments/_envcommon/`
- Local cluster uses Terraform to generate Vagrantfile + Ansible inventory
- AWS environments use standard TF modules (vpc, eks, rds, etc.)
- setup.sh wraps Terragrunt for convenience but doesn't replace it
