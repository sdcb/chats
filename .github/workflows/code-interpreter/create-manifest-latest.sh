#!/usr/bin/env bash
set -euo pipefail

REGISTRY="${REGISTRY:?REGISTRY env is required}"
NAMESPACE="${NAMESPACE:?NAMESPACE env is required}"
IMAGE_NAME="${IMAGE_NAME:?IMAGE_NAME env is required}"
RUN_NUMBER="${RUN_NUMBER:?RUN_NUMBER env is required}"

docker manifest create --amend "${REGISTRY}/${NAMESPACE}/${IMAGE_NAME}:latest" \
  "${REGISTRY}/${NAMESPACE}/${IMAGE_NAME}:r-${RUN_NUMBER}-linux-x64" \
  "${REGISTRY}/${NAMESPACE}/${IMAGE_NAME}:r-${RUN_NUMBER}-linux-arm64"

docker manifest push "${REGISTRY}/${NAMESPACE}/${IMAGE_NAME}:latest"
