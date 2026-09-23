FROM python:3.12-slim
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app
COPY pyproject.toml README.md ./
COPY lifelink_agents ./lifelink_agents
COPY coordinator_agent ./coordinator_agent
COPY domain_analysis_agent ./domain_analysis_agent
COPY dispatch_agent ./dispatch_agent
COPY validation_agent ./validation_agent
COPY tools ./tools
RUN pip install --no-cache-dir .
USER 10001
EXPOSE 8000
CMD ["uvicorn", "lifelink_agents.main:app", "--host", "0.0.0.0", "--port", "8000"]
