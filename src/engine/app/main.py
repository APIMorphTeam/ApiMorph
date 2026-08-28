from fastapi import FastAPI

from app.api import analyze, health

app = FastAPI(title="ApiMorph Engine", version="0.1.0-stage2")

app.include_router(health.router)
app.include_router(analyze.router)
