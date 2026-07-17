# escape=`

ARG PARENT_IMAGE
#[SXA] {
ARG SXA_RESOURCES_IMAGE
#[SXA] }

#[SXA] {
FROM ${SXA_RESOURCES_IMAGE} AS sxa_resources
#[SXA] }
FROM ${PARENT_IMAGE}

#[SXA] {
# COPY SXA
COPY --from=sxa_resources C:\module\solr\cores-sxa.json C:\data\cores-sxa.json
#[SXA] }
