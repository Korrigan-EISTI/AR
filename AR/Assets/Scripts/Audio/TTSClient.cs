using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class TTSClient : MonoBehaviour
{
    public string serverIP = "172.31.208.1";
    public int serverPort = 5002;
    public AudioSource audioSource;

    public void Speak(string text)
    {
        StartCoroutine(SendTextToTTS(text));
    }

    IEnumerator SendTextToTTS(string text)
    {
        byte[] audioBytes = null;

        try
        {
            using (var client = new TcpClient(serverIP, serverPort))
            using (var stream = client.GetStream())
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                stream.Write(textBytes, 0, textBytes.Length);
                client.Client.Shutdown(SocketShutdown.Send);

                using var ms = new MemoryStream();
                byte[] buffer = new byte[1024];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    ms.Write(buffer, 0, bytesRead);

                audioBytes = ms.ToArray();
            }
        }
        catch (SocketException ex)
        {
            Debug.LogWarning("[TTS] Erreur de connexion : " + ex.Message);
            yield break;
        }

        if (audioBytes == null || audioBytes.Length == 0)
        {
            Debug.LogWarning("[TTS] Aucun audio reçu.");
            yield break;
        }

        // Sauvegarder le MP3 dans un fichier temporaire
        string tempPath = Path.Combine(Application.persistentDataPath, "tts_temp.mp3");
        File.WriteAllBytes(tempPath, audioBytes);

        using var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG);
        yield return www.SendWebRequest();

        if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("[TTS] Erreur de lecture : " + www.error);
            yield break;
        }

        AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
        audioSource.clip = clip;
        audioSource.Play();
    }
}
