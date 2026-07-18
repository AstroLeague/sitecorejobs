# Docker Images for Sitecore Roles

This path contains build contexts for the local Sitecore XM1/SXA environment.
The `solution` image builds this repository's deployable artifacts, and the
role-specific images layer those artifacts and module assets onto the Sitecore
base images used by `docker-compose`.
