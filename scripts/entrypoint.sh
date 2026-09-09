#!/usr/bin/env ash

echo "run_id: $RUN_ID"

export HTTPS_PROXY=http://localhost:3128

dotnet test --test-modules TradeGateway.Tests.dll --results-directory reports --report-html --report-html-filename index.html || test_exit_code=$?

. "./scripts/publish-tests.sh"
publish_exit_code=$?

if [ $publish_exit_code -ne 0 ]; then
  echo "failed to publish test results"
  exit $publish_exit_code
fi

exit "${test_exit_code:-0}"
