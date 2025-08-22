#!/bin/bash

# Get the current directory
REPO_DIR=$(pwd)
echo "$REPO_DIR"

# Pull latest image
docker pull ghcr.io/super-linter/super-linter:latest

# Run the Docker container with the specified environment variables and volume mount
docker run \
	-e FILTER_REGEX_EXCLUDE='.*/wwwroot/.*' \
	-e IGNORE_GITIGNORED_FILES=true \
	-e LOG_LEVEL=INFO \
	-e DEFAULT_BRANCH=origin/develop \
	-e RUN_LOCAL=true \
	-e VALIDATE_CHECKOV=true \
	-e VALIDATE_ALL_CODEBASE=false \
	-v "$REPO_DIR:/tmp/lint" -it --rm ghcr.io/super-linter/super-linter:latest
