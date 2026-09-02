"""Optional LLM-assisted patch proposals (OpenAI-compatible API or Ollama)."""

from __future__ import annotations

import json
import os
import re
from dataclasses import dataclass

import httpx

from app.models.contracts import Finding

_SECRET_PATTERNS = [
    re.compile(r"sk_(live|test)_[A-Za-z0-9]+"),
    re.compile(r"rk_(live|test)_[A-Za-z0-9]+"),
    re.compile(r"ghp_[A-Za-z0-9]+"),
    re.compile(r"gho_[A-Za-z0-9]+"),
]


@dataclass(frozen=True)
class LlmSettings:
    enabled: bool
    provider: str
    model: str
    openai_api_key: str | None
    openai_base_url: str
    ollama_base_url: str
    timeout_seconds: float


def load_llm_settings() -> LlmSettings:
    enabled = os.getenv("LLM__ENABLED", os.getenv("LLM_ENABLED", "false")).lower() in {
        "1",
        "true",
        "yes",
    }
    provider = os.getenv("LLM__PROVIDER", os.getenv("LLM_PROVIDER", "ollama")).lower()
    model = os.getenv("LLM__MODEL", os.getenv("LLM_MODEL", "llama3.2"))
    openai_api_key = os.getenv("LLM__OPENAI_API_KEY") or os.getenv("OPENAI_API_KEY")
    openai_base_url = os.getenv("LLM__OPENAI_BASE_URL", os.getenv("OPENAI_BASE_URL", "https://api.openai.com/v1"))
    ollama_base_url = os.getenv("LLM__OLLAMA_BASE_URL", os.getenv("OLLAMA_BASE_URL", "http://127.0.0.1:11434"))
    timeout_seconds = float(os.getenv("LLM__TIMEOUT_SECONDS", "60"))

    return LlmSettings(
        enabled=enabled,
        provider=provider,
        model=model,
        openai_api_key=openai_api_key,
        ollama_base_url=ollama_base_url.rstrip("/"),
        openai_base_url=openai_base_url.rstrip("/"),
        timeout_seconds=timeout_seconds,
    )


def is_llm_configured(settings: LlmSettings | None = None) -> bool:
    resolved = settings or load_llm_settings()
    if not resolved.enabled:
        return False

    if resolved.provider == "openai":
        return bool(resolved.openai_api_key)

    return True


def propose_file_patch(
    *,
    relative_path: str,
    file_content: str,
    findings: list[Finding],
    settings: LlmSettings | None = None,
) -> str | None:
    """Return updated file content from the LLM, or None when LLM is unavailable."""
    resolved = settings or load_llm_settings()
    if not is_llm_configured(resolved):
        return None

    snippet = _build_context_snippet(file_content, findings)
    prompt = _build_prompt(relative_path, snippet, findings)
    response_text = _call_llm(prompt, resolved)
    if not response_text:
        return None

    return _extract_file_content(response_text) or None


def _build_context_snippet(file_content: str, findings: list[Finding]) -> str:
    lines = file_content.splitlines()
    selected: set[int] = set()

    for finding in findings:
        start = max(1, finding.line - 8)
        end = min(len(lines), finding.line + 8)
        selected.update(range(start, end + 1))

    if not selected:
        return _redact_secrets(file_content[:4000])

    snippet_lines = []
    for line_number in sorted(selected):
        snippet_lines.append(f"{line_number:04d}| {lines[line_number - 1]}")
    return _redact_secrets("\n".join(snippet_lines))


def _build_prompt(relative_path: str, snippet: str, findings: list[Finding]) -> str:
    finding_lines = "\n".join(
        f"- {finding.rule_id} at line {finding.line}: {finding.message} ({finding.evidence})"
        for finding in findings
    )
    return (
        "You are ApiMorph, a migration assistant for Stripe API changes in C#.\n"
        "Return ONLY valid JSON with this shape:\n"
        '{"patchedContent":"<full updated C# file>"}\n'
        "Do not add markdown fences. Keep changes minimal and safe.\n"
        f"File: {relative_path}\n"
        f"Findings:\n{finding_lines}\n"
        f"Context:\n{snippet}\n"
    )


def _call_llm(prompt: str, settings: LlmSettings) -> str | None:
    try:
        if settings.provider == "openai":
            return _call_openai_compatible(prompt, settings, settings.openai_base_url, settings.openai_api_key)
        return _call_ollama(prompt, settings)
    except (httpx.HTTPError, json.JSONDecodeError, KeyError):
        return None


def _call_openai_compatible(
    prompt: str,
    settings: LlmSettings,
    base_url: str,
    api_key: str | None,
) -> str | None:
    if not api_key:
        return None

    payload = {
        "model": settings.model,
        "messages": [
            {"role": "system", "content": "You produce safe C# migration patches as JSON only."},
            {"role": "user", "content": prompt},
        ],
        "temperature": 0.1,
    }

    with httpx.Client(timeout=settings.timeout_seconds) as client:
        response = client.post(
            f"{base_url}/chat/completions",
            headers={"Authorization": f"Bearer {api_key}"},
            json=payload,
        )
        response.raise_for_status()
        body = response.json()
        return body["choices"][0]["message"]["content"]


def _call_ollama(prompt: str, settings: LlmSettings) -> str | None:
    payload = {
        "model": settings.model,
        "messages": [
            {"role": "system", "content": "You produce safe C# migration patches as JSON only."},
            {"role": "user", "content": prompt},
        ],
        "stream": False,
        "options": {"temperature": 0.1},
    }

    with httpx.Client(timeout=settings.timeout_seconds) as client:
        response = client.post(f"{settings.ollama_base_url}/api/chat", json=payload)
        response.raise_for_status()
        body = response.json()
        return body["message"]["content"]


def _extract_file_content(response_text: str) -> str | None:
    cleaned = response_text.strip()
    if cleaned.startswith("```"):
        cleaned = re.sub(r"^```(?:json)?\s*", "", cleaned)
        cleaned = re.sub(r"\s*```$", "", cleaned)

    try:
        payload = json.loads(cleaned)
    except json.JSONDecodeError:
        return None

    patched = payload.get("patchedContent")
    return patched if isinstance(patched, str) and patched.strip() else None


def _redact_secrets(value: str) -> str:
    redacted = value
    for pattern in _SECRET_PATTERNS:
        redacted = pattern.sub("[REDACTED]", redacted)
    return redacted
