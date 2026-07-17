# Configuration Project
This project is the solution for deploying all projects with one msbuild operation.
* All projects must be referenced by this project
* This project has a hook on Build to run through all project references and run the publish profile for all.
* Simply BUILD this project to build and publish all (referenced) projects to target folder
* New projects are auto-added as a reference based on the patterns defined in Directory.Build.props
* CI/CD pipeline uses this but with the DevOps publish profile which runs with Release configruation

# Web.config
Web.config is only deployed locally to docker container.
All other environments must either be edited manually, or managed via CI/CD pipeline.
(see conditional element in .csproj)