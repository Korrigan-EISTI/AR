import socket
import torch
import io
import json
from PIL import Image

model = torch.hub.load("ultralytics/yolov5", "yolov5s", pretrained=True)
model.eval()

def handle_client(conn):
    data = b""
    while True:
        chunk = conn.recv(4096)
        if not chunk: break
        data += chunk

    img = Image.open(io.BytesIO(data)).convert("RGB")
    results = model(img)

    best = None
    max_conf = 0

    for row in results.pandas().xyxy[0].to_dict(orient="records"):
        conf = float(row["confidence"])
        if conf > max_conf:
            max_conf = conf
            best = {
                "xmin": float(row["xmin"]),
                "ymin": float(row["ymin"]),
                "xmax": float(row["xmax"]),
                "ymax": float(row["ymax"]),
                "confidence": conf,
                "name": row["name"]
            }

    if best:
        response = json.dumps(best)
        conn.sendall(response.encode("utf-8"))

with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
    s.bind(("0.0.0.0", 5001))
    s.listen()
    print("[Server] Listening on port 5001")
    while True:
        conn, addr = s.accept()
        print(f"[Server] Connected from {addr}")
        try:
            handle_client(conn)
        finally:
            conn.close()
