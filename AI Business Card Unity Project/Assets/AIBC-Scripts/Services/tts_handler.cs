using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IBM.Watson.DeveloperCloud.Services.TextToSpeech.v1;
using IBM.Watson.DeveloperCloud.Utilities;
using IBM.Watson.DeveloperCloud.Logging;
using IBM.Watson.DeveloperCloud.Connection;

public class tts_handler : MonoBehaviour {

    private TextToSpeech _textToSpeech;

    private string tts_username = "23225016-db41-4a1a-bccc-af5a34d26e86";
    private string tts_password = "kURHMaq8hmyr";
    private string tts_url = "https://stream.watsonplatform.net/text-to-speech/api";

    private bool ttsResponseReceived;

    private AudioClip lastClip;

    private bool isPlaying;

    //private string tts_output;

    void Start () {
        LogSystem.InstallDefaultReactors();

        Credentials credentials = new Credentials(tts_username, tts_password, tts_url);
        _textToSpeech = new TextToSpeech(credentials);

        _textToSpeech.Voice = VoiceType.en_GB_Kate;
        //_textToSpeech.AudioFormat = AudioFormatType.FLAC; // lossless but compressed

        ttsResponseReceived = false;
        isPlaying = false;
    }

    public void Synthesize(string input)
    {
        TextToSpeech t = new TextToSpeech(new Credentials(tts_username, tts_password, tts_url));

        if (!_textToSpeech.ToSpeech(OnSynthesize, OnFail, input))
            Log.Debug("TTS.ToSpeech()", "Failed to synthesize!");
    }

    private void OnSynthesize(AudioClip clip, Dictionary<string, object> customData)
    {
        lastClip = clip;
        ttsResponseReceived = true;
        //PlayClip(clip);
    }

    private void OnFail(RESTConnector.Error error, Dictionary<string, object> customData)
    {
        Log.Error("TTS.OnFail()", "Error received: {0}", error.ToString());
    }

    public AudioClip getLastTtsResponse()
    {
        return lastClip;
    }

    public bool hasNextTtsResponse()
    {
        return ttsResponseReceived;
    }

    public void waitForNextTtsResponse()
    {
        ttsResponseReceived = false;
    }

    private void PlayClip(AudioClip clip)
    {
        if (Application.isPlaying && clip != null)
        {

            GameObject audioObject = new GameObject("AudioObject");
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.spatialBlend = 0.0f;
            source.loop = false;
            source.clip = clip;
            source.Play();

            Destroy(audioObject, clip.length);
        }
    }

}
