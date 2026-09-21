using UnityEngine;

// 절차적 효과음 (M10) - 별도 오디오 파일 없이 코드로 사인파 톤을 합성해서 재생
// 정시 배달 완료 시 경쾌한 2음 "딩동" 소리, 많이 늦었을 때 아래로 처지는 아쉬운 소리
public static class Sfx
{
    private static AudioClip successClip;
    private static AudioClip lateClip;

    public static void PlaySuccess(Vector3 position)
    {
        if (successClip == null)
        {
            successClip = BuildChime(new float[] { 880f, 1318.5f }, 0.12f); // A5 -> E6
        }
        AudioSource.PlayClipAtPoint(successClip, position, 0.6f);
    }

    public static void PlayLate(Vector3 position)
    {
        if (lateClip == null)
        {
            lateClip = BuildChime(new float[] { 220f, 174.6f }, 0.18f); // A3 -> F3
        }
        AudioSource.PlayClipAtPoint(lateClip, position, 0.5f);
    }

    // 지정한 주파수들을 순서대로 짧게 이어붙여서 톤을 합성함 (음마다 사인 곡선 볼륨 페이드를 줘서 클릭 잡음 방지)
    private static AudioClip BuildChime(float[] frequencies, float noteDuration)
    {
        int sampleRate = 44100;
        int samplesPerNote = Mathf.RoundToInt(sampleRate * noteDuration);
        int totalSamples = samplesPerNote * frequencies.Length;
        float[] data = new float[totalSamples];

        for (int n = 0; n < frequencies.Length; n++)
        {
            float freq = frequencies[n];
            for (int i = 0; i < samplesPerNote; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * i / samplesPerNote); // 0에서 시작해 0으로 끝나는 부드러운 볼륨 곡선
                data[n * samplesPerNote + i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.5f;
            }
        }

        AudioClip clip = AudioClip.Create("Sfx", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
