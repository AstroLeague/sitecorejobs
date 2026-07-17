# escape=`

ARG PARENT_IMAGE
ARG SPE_RESOURCES_IMAGE

FROM ${SPE_RESOURCES_IMAGE} AS spe_resources
FROM ${PARENT_IMAGE}

SHELL ["powershell", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Copy SPE
COPY --from=spe_resources C:\module\db C:\resources\spe