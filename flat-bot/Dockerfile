FROM python:3.12-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY flat_bot ./flat_bot

ENV PYTHONUNBUFFERED=1
CMD ["python", "-m", "flat_bot"]
