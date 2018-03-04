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

    private string tts_username = "7c903886-153a-44b0-ad64-26081c1a5910";
    private string tts_password = "65IXm0UXkZr8";
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

        //_textToSpeech.AudioFormat = AudioFormatType.FLAC; // lossless but compressed - currently not working

        ttsResponseReceived = false;
        isPlaying = false;
    }

    public void Synthesize(string input)
    {
        if (!_textToSpeech.ToSpeech(OnSynthesize, OnFail, input))
            Log.Debug("TTS.ToSpeech()", "Failed to synthesize!");
    }

    private void OnSynthesize(AudioClip clip, Dictionary<string, object> customData)
    {
        lastClip = clip;
        ttsResponseReceived = true;
        //PlayClip(clip); //now handled by ServiceManager
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

    /* Moved to ServiceManager */
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
