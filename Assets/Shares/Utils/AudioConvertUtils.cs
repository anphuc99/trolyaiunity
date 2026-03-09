using System;
using System.Text;
using UnityEngine;

namespace Share.Utils
{
	/// <summary>
	/// Shared audio conversion utility methods.
	/// </summary>
	public static class AudioConvertUtils
	{
		/// <summary>
		/// Trims an <see cref="AudioClip"/> to the specified number of recorded samples.
		/// </summary>
		/// <param name="sourceClip">Full-length recording clip.</param>
		/// <param name="sampleCount">Actual sample count recorded.</param>
		/// <returns>Trimmed AudioClip, or null on failure.</returns>
		public static AudioClip TrimAudioClip(AudioClip sourceClip, int sampleCount)
		{
			if (sourceClip == null || sampleCount <= 0)
			{
				return null;
			}

			sampleCount = Mathf.Clamp(sampleCount, 1, sourceClip.samples);
			var channelCount = sourceClip.channels;
			var sourceData = new float[sourceClip.samples * channelCount];
			sourceClip.GetData(sourceData, 0);

			var trimmedData = new float[sampleCount * channelCount];
			Array.Copy(sourceData, trimmedData, trimmedData.Length);

			var trimmedClip = AudioClip.Create("chat-recorded", sampleCount, channelCount, sourceClip.frequency, false);
			trimmedClip.SetData(trimmedData, 0);
			return trimmedClip;
		}

		/// <summary>
		/// Converts an <see cref="AudioClip"/> to a standard 16-bit PCM WAV byte array.
		/// </summary>
		/// <param name="clip">Source AudioClip.</param>
		/// <returns>WAV byte array, or null on failure.</returns>
		public static byte[] ConvertClipToWav(AudioClip clip)
		{
			if (clip == null)
			{
				return null;
			}

			var sampleCount = clip.samples;
			var channelCount = clip.channels;
			var frequency = clip.frequency;
			var samples = new float[sampleCount * channelCount];
			clip.GetData(samples, 0);

			var pcmBytes = new byte[samples.Length * 2];
			for (var index = 0; index < samples.Length; index++)
			{
				var value = Mathf.Clamp(samples[index], -1f, 1f);
				short pcmValue = (short)Mathf.RoundToInt(value * short.MaxValue);
				pcmBytes[index * 2] = (byte)(pcmValue & 0xff);
				pcmBytes[index * 2 + 1] = (byte)((pcmValue >> 8) & 0xff);
			}

			var headerSize = 44;
			var wavBytes = new byte[headerSize + pcmBytes.Length];

			Encoding.ASCII.GetBytes("RIFF").CopyTo(wavBytes, 0);
			BitConverter.GetBytes(wavBytes.Length - 8).CopyTo(wavBytes, 4);
			Encoding.ASCII.GetBytes("WAVE").CopyTo(wavBytes, 8);
			Encoding.ASCII.GetBytes("fmt ").CopyTo(wavBytes, 12);
			BitConverter.GetBytes(16).CopyTo(wavBytes, 16);
			BitConverter.GetBytes((short)1).CopyTo(wavBytes, 20);
			BitConverter.GetBytes((short)channelCount).CopyTo(wavBytes, 22);
			BitConverter.GetBytes(frequency).CopyTo(wavBytes, 24);
			BitConverter.GetBytes(frequency * channelCount * 2).CopyTo(wavBytes, 28);
			BitConverter.GetBytes((short)(channelCount * 2)).CopyTo(wavBytes, 32);
			BitConverter.GetBytes((short)16).CopyTo(wavBytes, 34);
			Encoding.ASCII.GetBytes("data").CopyTo(wavBytes, 36);
			BitConverter.GetBytes(pcmBytes.Length).CopyTo(wavBytes, 40);
			pcmBytes.CopyTo(wavBytes, headerSize);

			return wavBytes;
		}
	}
}
