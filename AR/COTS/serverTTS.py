import socket
from gtts import gTTS
import io

HOST = '0.0.0.0'
PORT = 5002

def handle_client(conn):
    print("[TTS SERVER] Client connecté.")

    try:
        # Lire le texte du client
        data = b""
        while True:
            chunk = conn.recv(1024)
            if not chunk:
                break
            data += chunk

        text = data.decode('utf-8').strip()
        print(f"[TTS SERVER] Texte reçu : {text}")

        if not text:
            print("[TTS SERVER] Texte vide.")
            return

        # Générer l'audio MP3 avec gTTS
        tts = gTTS(text=text, lang='fr')
        mp3_fp = io.BytesIO()
        tts.write_to_fp(mp3_fp)
        mp3_bytes = mp3_fp.getvalue()

        print(f"[TTS SERVER] MP3 généré ({len(mp3_bytes)} octets)")

        # Envoyer les données MP3
        conn.sendall(mp3_bytes)
        print("[TTS SERVER] Audio envoyé au client.")

    except Exception as e:
        print(f"[TTS SERVER] Erreur : {e}")
    finally:
        conn.close()
        print("[TTS SERVER] Connexion fermée.\n")


def start_server():
    print(f"[TTS SERVER] En écoute sur {HOST}:{PORT}...")
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.bind((HOST, PORT))
        server.listen(1)

        while True:
            conn, addr = server.accept()
            print(f"[TTS SERVER] Connexion de {addr}")
            handle_client(conn)


if __name__ == "__main__":
    start_server()
