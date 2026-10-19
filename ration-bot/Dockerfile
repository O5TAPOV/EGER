FROM python:3.12-slim

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY ration_bot ./ration_bot

ENV PYTHONUNBUFFERED=1
CMD ["python", "-m", "ration_bot"]
