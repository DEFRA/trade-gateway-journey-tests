#!/usr/bin/env ash

echo "run_id: $RUN_ID"

export HTTPS_PROXY=http://localhost:3128

mkdir -p reports

dotnet test TradeGateway.Tests.dll --logger "trx;LogFileName=test.trx" --results-directory "reports" || test_exit_code=$?

dotnet tool run trxlog2html -i "reports/test.trx" -o "reports/index.html"

. "./scripts/publish-tests.sh"
publish_exit_code=$?

if [ $publish_exit_code -ne 0 ]; then
  echo "failed to publish test results"
  exit $publish_exit_code
fi

exit "${test_exit_code:-0}"
