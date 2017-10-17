using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Automatic Time Stamp by Iman.
/// </summary>
public class AutomaticTimeStamp : MonoBehaviour {

	// The sound to be processed.
	public AudioSource TheSound;
	// Result file name to be saved.
	public string FileName;
	// Info text object.
	public Text Infos;
	// Array size for spectrum data.
	public int ArraySize;
	// Visualizer dot width.
	public int DotWidth;
	// The threshold that decide whether the peak is at the attack phase or not.
	public float[] Threshold; // For each sound (kick, hi-hat, snare).
	// This is the considered group of frequency where a specific sound played.
	public float[] ArrayGroupCoefs; // Array group for each sound.
	// This is the considered minimal distance between current and previous peak.
	public float[] MinDistances;
	// Important variables.
	private int SoundSamplesFreq;
	private int TotalChannel;
	private int[] TotalAttackDetected; // For each sound (kick, snare, hi-hat).
	private float[] MaxPeak; // For each sound (kick, snare, hi-hat).
	private float[] PrevMaxPeak; // Previous max peak for each sound (kick, snare, hi-hat).
	private List<float>[] TimeStampResult; // For each sound (kick, snare, hi-hat).
	private Vector2 ScreenRes;
	private float[] SpectrumData1;
	private float[] SpectrumData2;
	private Vector2[] ArrayGroup; // The array group for each sound.
	// Process states.
	private bool AttackDetectionState, SavingResultState;
	// Finish dialog object.
	public GameObject FinishDialog;
	// Progress bar objects.
	public GameObject AttackDetectionProgress, TimeStampingProgress, SavingResultProgress;

	// ----------------------------------------------------------------------------------

	void Start () { // Initialization.
		Initialization();
	}

	void Update () { // Our main loop process.
		if(AttackDetectionState)
			AttackDetection();
		else if(SavingResultState)
			SavingResult(FileName);
			
		ShowInfo();
	}
		
	private void OnGUI () { // The visualization interface.
		DrawVisualization();
	}

	// ----------------------------------------------------------------------------------

	private void Initialization () {
		// Hide finish dialog.
		FinishDialog.SetActive(false);
		// Find the sound frequency.
		SoundSamplesFreq = TheSound.clip.frequency;
		// Initialize spectrum arrays.
		SpectrumData1 = new float[ArraySize];
		SpectrumData2 = new float[ArraySize];
		// Find total channel.
		TotalChannel = TheSound.clip.channels;
		// Initialize other variables.
		TotalAttackDetected = new int[3];
		MaxPeak = new float[3];
		PrevMaxPeak = new float[3];
		AttackDetectionState = SavingResultState = false;
		TimeStampResult = new List<float>[3];
		TimeStampResult[0] = new List<float>();
		TimeStampResult[1] = new List<float>();
		TimeStampResult[2] = new List<float>();
		// Array group that contains the considered frequency for kick, snare, and hi-hat.
		ArrayGroup = new Vector2[3];
		ArrayGroup[0] = new Vector2(0f,(ArrayGroupCoefs[0]/64f)*ArraySize);
		ArrayGroup[1] = new Vector2(ArrayGroup[0].y,(ArrayGroupCoefs[1]/64f)*ArraySize);
		ArrayGroup[2] = new Vector2(ArrayGroup[1].y,(ArrayGroupCoefs[2]/64f)*ArraySize);
		// Play the sound after 3 seconds.
		StartCoroutine("PlaySound");
	}

	private IEnumerator PlaySound () {
		yield return new WaitForSeconds(1.5f);
		TheSound.Play();
		AttackDetectionState = true;
	}

	private void ShowInfo () {
		Infos.text = "Sound Samples Frequency : " + SoundSamplesFreq.ToString();
		Infos.text += "\t\t\t\tOutput Sample Rate : " + AudioSettings.outputSampleRate.ToString();
		Infos.text += "\nCurrent Screen Resolution : " + ScreenRes.ToString();
		Infos.text += "\t\t\t\tTotal Channel : " + TotalChannel.ToString();
		Infos.text += "\n\nKick Array group : " + ArrayGroup[0].ToString();
		Infos.text += "\t\t\tTotal Kick Attack Detected : " + TotalAttackDetected[0].ToString();
		Infos.text += "\nSnare Array group : " + ArrayGroup[1].ToString();
		Infos.text += "\t\t\tTotal Snare Attack Detected : " + TotalAttackDetected[1].ToString();
		Infos.text += "\nHi-hat Array group : " + ArrayGroup[2].ToString();
		Infos.text += "\t\t\tTotal Hi-hat Attack Detected : " + TotalAttackDetected[2].ToString();
	}

	/// <summary>
	/// Draws the visualization as square pixels int the middle of the screen.
	/// </summary>
	private void DrawVisualization () {
		// current screen resolution.
		ScreenRes = new Vector2((float)Screen.width,(float)Screen.height);
		
		if(TheSound.isPlaying) {
			// Draw spectrum in the middle of the screen.
			for(int a = 0; a < SpectrumData1.Length; a++) {
				// line width, texture object, and color.
				Texture2D lineTex = Texture2D.whiteTexture;
				// it's normalized from 0 to 1 based on current screen resolution.
				Vector2 linePos = new Vector2();
				linePos.x = (float)(a+1)/(SpectrumData1.Length);
				linePos.y = 0.5f;
				
				GUI.color = Color.red;
				GUI.DrawTexture(
					new Rect(linePos.x*ScreenRes.x,
						(linePos.y-(SpectrumData1[a]))*ScreenRes.y,
						DotWidth,
						DotWidth),
					lineTex);
				GUI.color = Color.yellow;
				GUI.DrawTexture(
					new Rect(linePos.x*ScreenRes.x,
						(linePos.y+(SpectrumData2[a]+0.01f))*ScreenRes.y,
						DotWidth,
						DotWidth),
					lineTex);
				if(a%(ArraySize/8) == 0) {
					GUI.color = Color.white;
					GUI.DrawTexture(
						new Rect(linePos.x*ScreenRes.x,
							0.375f*ScreenRes.y,
							DotWidth,
							ScreenRes.y/4f),
						lineTex);
				}
			}
		}
	}

	/// <summary>
	/// Attack detection process. Started with getting spectrum data then extract it.
	/// </summary>
	private void AttackDetection () {
		GetSoundSpectrum();
		ExtractSoundSpectrum();
	}

	private void GetSoundSpectrum () {
		// Get spectrum data of channel 0.
		TheSound.GetSpectrumData(SpectrumData1,0,FFTWindow.BlackmanHarris);
		// Get spectrum data of channel 1.
		if(TotalChannel>1)
			TheSound.GetSpectrumData(SpectrumData2,1,FFTWindow.BlackmanHarris);
	}

	private void ExtractSoundSpectrum () {
		// Find max peak for each sound.
		FindMaxPeak();
		// Classified the sound as kick, snare, or hi-hat.
		Considering();
	}

	/// <summary>
	/// Finds the max peak of each sound of spectrum data.
	/// </summary>
	private void FindMaxPeak () {
		for(int a=0; a<MaxPeak.Length; a++) {
			// Save previous max peaks.
			PrevMaxPeak[a] = MaxPeak[a];
			// Reset max peak.
			MaxPeak[a] = 0f;
		}

		float[] maxValue = new float[3];
		// For all index in array, we should know which group is kick, snare, or hi-hat.
		// Then we can find the max peak out of it.
		for(int a=0; a<3; a++) {
			for(int b=(int)ArrayGroup[a].x; b<(int)ArrayGroup[a].y; b++) {
				maxValue[a] = Mathf.Max(SpectrumData1[b],SpectrumData2[b]);
				if(maxValue[a] > MaxPeak[a])
					MaxPeak[a] = maxValue[a];
			}
		}
	}

	/// <summary>
	/// Considering the max peak, does it meet the threshold or not. And when it does
	/// meet the threshold, check further does it greater than the previous max peak or not.
	/// </summary>
	private void Considering () {
		for(int a=0; a<MaxPeak.Length; a++) {
			if(MaxPeak[a] > Threshold[a]) {
				if( (MaxPeak[a] > PrevMaxPeak[a]) && 
					((MaxPeak[a]-PrevMaxPeak[a]) > MinDistances[a])) {
					// Increase counter.
					TotalAttackDetected[a] ++;
					// Save the time stamp.
					TimeStamping(a,TheSound.time);
				}
			}
		}
		// Update progress bar in UI.
		AttackDetectionProgress.transform.localScale = new Vector3(TheSound.time/TheSound.clip.length,1f,1f);
		// Ends the attack detection process.
		if(!TheSound.isPlaying) {
			// Fulling the progress bar.
			AttackDetectionProgress.transform.localScale = new Vector3(1f,1f,1f);
			TimeStampingProgress.transform.localScale = new Vector3(1f,1f,1f);
			// Switch the states.
			AttackDetectionState = false;
			SavingResultState = true;
		}
	}

	/// <summary>
	/// Time stamping process. Here we just save the time where attack was detected into a list.
	/// </summary>
	private void TimeStamping (int index, float value) {
		TimeStampResult[index].Add(value);
		// Update progress bar in UI.
		TimeStampingProgress.transform.localScale = new Vector3(TheSound.time/TheSound.clip.length,1f,1f);
	}

	/// <summary>
	/// Saving the result into an xml file.
	/// </summary>
	/// <param name="fileName">File name.</param>
	private void SavingResult (string fileName) {
		// Update progress bar.
		SavingResultProgress.transform.localScale = new Vector3(0.5f,1f,1f);

		XmlSerializer serializer = new XmlSerializer(typeof(List<float>[]));
		TextWriter writeResult = new StreamWriter(fileName);

		serializer.Serialize(writeResult,TimeStampResult);
		writeResult.Close();

		// Update progress bar.
		SavingResultProgress.transform.localScale = new Vector3(1f,1f,1f);
		// Change the process state.
		SavingResultState = false;
		// Show finish dialog.
		FinishDialog.SetActive(true);
	}
}
