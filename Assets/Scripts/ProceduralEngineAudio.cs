using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class ProceduralEngineAudio : MonoBehaviour
{
    [SerializeField] private float baseFrequency = 80f;
    [SerializeField] private float rpmFrequency = 160f;

    private AudioSource source;
    private Rigidbody body;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        body = GetComponentInParent<Rigidbody>();
        source.loop = true;
        source.spatialBlend = 0.85f;
        source.clip = CreateEngineClip();
        source.Play();
    }

    private void Update()
    {
        if (body == null)
        {
            return;
        }

        var speed01 = Mathf.Clamp01(body.velocity.magnitude / 22f);
        source.pitch = Mathf.Lerp(0.75f, 1.45f, speed01);
        source.volume = Mathf.Lerp(0.2f, 0.7f, speed01);
    }

    private AudioClip CreateEngineClip()
    {
        const int sampleRate = 44100;
        const int lengthSeconds = 2;
        var samples = new float[sampleRate * lengthSeconds];

        for (var i = 0; i < samples.Length; i++)
        {
            var t = (float)i / sampleRate;
            var low = Mathf.Sin(2f * Mathf.PI * baseFrequency * t) * 0.35f;
            var high = Mathf.Sin(2f * Mathf.PI * rpmFrequency * t) * 0.12f;
            samples[i] = low + high;
        }

        var clip = AudioClip.Create("ProceduralEngineLoop", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
