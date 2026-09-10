#!/usr/bin/env python3
"""Automated provider-pack harness + printable manual wizard checklist.

Run from repo root:

  python scripts/test_provider_packs.py
  python scripts/test_provider_packs.py --pytest-only
  python scripts/test_provider_packs.py --checklist
"""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ENGINE = ROOT / "src" / "engine"
PROVIDERS = ROOT / "providers"

sys.path.insert(0, str(ENGINE))

from app.models.contracts import AnalyzeOptions, AnalyzeRequest  # noqa: E402
from app.services.analyzer import analyze_repository  # noqa: E402
from app.services.provider_pack import (  # noqa: E402
    ALLOWED_SOURCE_TYPES,
    MANDATORY_RELATIVE_FILES,
    validate_provider_pack_dir,
    validate_shipped_providers,
)


def _errors(issues):
    return [i for i in issues if i.code != "warning"]


def run_shipped_validation() -> int:
    print("== Shipped packs under providers/ ==")
    results = validate_shipped_providers(PROVIDERS)
    if not results:
        print("FAIL: no packs found")
        return 1

    failed = 0
    for name, issues in results.items():
        errs = _errors(issues)
        warns = [i for i in issues if i.code == "warning"]
        status = "OK" if not errs else "FAIL"
        print(f"  [{status}] {name}")
        for relative in MANDATORY_RELATIVE_FILES:
            exists = (PROVIDERS / name / relative).is_file()
            print(f"         {'OK' if exists else 'MISSING'} {relative}")
            if not exists:
                failed += 1
        for issue in errs:
            print(f"         ERROR {issue}")
            failed += 1
        for issue in warns:
            print(f"         WARN  {issue}")
    return 1 if failed else 0


def run_source_type_matrix() -> int:
    print("\n== Source-type matrix (temp packs) ==")
    openapi = {
        "openapi": "3.0.0",
        "info": {"title": "Harness", "version": "1"},
        "paths": {
            "/v1/ping": {
                "get": {
                    "operationId": "Ping",
                    "responses": {"200": {"description": "ok"}},
                }
            }
        },
    }

    cases: list[tuple[str, dict, bool]] = [
        ("manual", {"type": "manual"}, True),
        ("openapi_url", {"type": "openapi_url", "openapiUrl": "https://example.com/openapi.json"}, True),
        (
            "github",
            {
                "type": "github",
                "gitUrl": "https://github.com/acme/api.git",
                "gitPath": "openapi.json",
            },
            True,
        ),
        (
            "gitlab",
            {
                "type": "gitlab",
                "gitUrl": "https://gitlab.com/acme/api.git",
                "gitPath": "spec/openapi.json",
            },
            True,
        ),
        ("git", {"type": "git", "gitUrl": "https://git.example.com/api.git", "gitPath": "openapi.json"}, True),
        (
            "html_changelog",
            {"type": "html_changelog", "changelogUrl": "https://example.com/changelog"},
            True,
        ),
        ("openapi_url_missing", {"type": "openapi_url"}, False),
        ("github_missing", {"type": "github"}, False),
        ("html_missing", {"type": "html_changelog"}, False),
        ("bad_type", {"type": "rss"}, False),
    ]

    failed = 0
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        for index, (label, source, expect_ok) in enumerate(cases):
            folder_id = re.sub(r"[^a-z0-9_]", "_", f"case{index}_{label}".lower())
            if not folder_id[0].isalpha():
                folder_id = "c" + folder_id

            pack = root / folder_id
            (pack / "contract").mkdir(parents=True)
            (pack / "rules").mkdir(parents=True)
            (pack / "manifest.json").write_text(
                json.dumps(
                    {
                        "id": folder_id,
                        "displayName": label,
                        "languages": ["csharp"],
                        "source": source,
                        "feed": {"enabled": False, "fingerprintFile": "contract/target.json"},
                    }
                ),
                encoding="utf-8",
            )
            (pack / "contract" / "baseline.json").write_text(json.dumps(openapi), encoding="utf-8")
            (pack / "contract" / "target.json").write_text(json.dumps(openapi), encoding="utf-8")
            (pack / "rules" / "csharp.json").write_text("[]", encoding="utf-8")
            (pack / "PROVIDER.md").write_text(f"# {label}\n", encoding="utf-8")

            issues = validate_provider_pack_dir(pack)
            ok = len(_errors(issues)) == 0
            status = "OK" if ok == expect_ok else "FAIL"
            if status == "FAIL":
                failed += 1
            print(f"  [{status}] {label:22} expect_ok={expect_ok} actual_ok={ok}")
            if status == "FAIL":
                for issue in issues:
                    print(f"           {issue}")

    return 1 if failed else 0


def run_mock_analyze() -> int:
    print("\n== Analyze mock demo fixture ==")
    repo = ENGINE / "fixtures" / "mock-demo"
    try:
        result = analyze_repository(
            AnalyzeRequest(
                contractVersion="1",
                provider="mock",
                repositoryPath=str(repo),
                language="csharp",
                options=AnalyzeOptions(detectOnly=True, llmEnabled=False),
            )
        )
    except Exception as exc:  # noqa: BLE001
        print(f"  [FAIL] analyze raised: {exc}")
        return 1

    count = result.summary.finding_count
    print(f"  [OK] findings={count}")
    for finding in result.findings[:5]:
        print(f"       - {finding.rule_id} @ {finding.file_path}:{finding.line}")
    return 0 if count >= 1 else 1


def run_pytest() -> int:
    print("\n== pytest provider suites ==")
    cmd = [
        sys.executable,
        "-m",
        "pytest",
        str(ENGINE / "tests" / "test_provider_pack.py"),
        str(ENGINE / "tests" / "test_provider_validation.py"),
        "-q",
    ]
    proc = subprocess.run(cmd, cwd=str(ENGINE), check=False)
    return proc.returncode


def print_checklist() -> None:
    sources = ", ".join(sorted(ALLOWED_SOURCE_TYPES))
    print(
        f"""
== Manual wizard / operator checklist (Stage 9) ==

Allowed source types: {sources}

A. CLI wizard (custom / manual)
  [ ] dotnet run --project src/ApiMorph.Cli -- provider init myvendor --display-name "My Vendor"
  [ ] Folder created: deploy/config/providers/myvendor/
  [ ] Mandatory files: manifest.json, contract/baseline.json, contract/target.json
  [ ] PROVIDER.md present (agent instructions)
  [ ] Edit baseline/target OpenAPI (or keep stubs)
  [ ] Optional: rules/csharp.json
  [ ] dotnet run --project src/ApiMorph.Cli -- provider validate myvendor
  [ ] Set repo.providers = myvendor in repos.d
  [ ] Scan with --provider myvendor

B. OpenAPI URL source (metadata + local contracts)
  [ ] --source openapi_url --openapi-url https://example.com/openapi.json
  [ ] validate PASS only when URL is set
  [ ] Keep local contract/ copies for scanning

C. GitHub / GitLab / git
  [ ] --source github --git-url https://github.com/org/repo.git --git-path path/to/openapi.json
  [ ] validate fails if gitUrl missing
  [ ] Pin/copy OpenAPI into contract/ for scan

D. HTML changelog (assist-only)
  [ ] --source html_changelog --changelog-url https://example.com/changelog
  [ ] validate warns assist-only
  [ ] Human/agent updates contract/target.json after reading the page

E. Automated regression
  [ ] python scripts/test_provider_packs.py
  [ ] provider list shows stripe + mock

F. Negative cases (expect FAIL)
  [ ] Delete contract/target.json -> missing_file
  [ ] manifest.id != folder -> id_mismatch
  [ ] source.type = soap -> invalid_source
  [ ] Broken regex in csharp.json -> invalid_rules
"""
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="ApiMorph provider pack test harness")
    parser.add_argument("--pytest-only", action="store_true")
    parser.add_argument("--checklist", action="store_true", help="Print manual checklist only")
    parser.add_argument("--skip-pytest", action="store_true")
    args = parser.parse_args()

    if args.checklist:
        print_checklist()
        return 0

    if args.pytest_only:
        return run_pytest()

    code = 0
    code |= run_shipped_validation()
    code |= run_source_type_matrix()
    code |= run_mock_analyze()
    if not args.skip_pytest:
        code |= run_pytest()
    print_checklist()
    print("\nHarness result:", "PASS" if code == 0 else "FAIL")
    return code


if __name__ == "__main__":
    raise SystemExit(main())
