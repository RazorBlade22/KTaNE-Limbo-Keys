using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class LimboKeysScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable Selectable;
    public SpriteRenderer[] Keys;
    public SpriteRenderer Focus;

    private KMAudio.KMAudioRef Sound;
    private List<Vector3> InitKeyPositions = new List<Vector3>();
    private List<int> RotationIDs = new List<int>();
    private int SwapPos;

    private List<List<int>> StandardSwaps = new List<List<int>>()
    {
        new List<int>() { 1, 2, 3, 4, 5, 6, 7, 0 },     //Clockwise belt
        new List<int>() { 7, 0, 1, 2, 3, 4, 5, 6 },     //Anticlockwise belt
        new List<int>() { 7, 6, 5, 4, 3, 2, 1, 0 },     //Column swaps
        new List<int>() { 1, 6, 3, 4, 5, 2, 7, 0 },     //Top half clockwise, bottom half clockwise
        new List<int>() { 1, 6, 5, 2, 3, 4, 7, 0 },     //Top half clockwise, bottom half anticlockwise
        new List<int>() { 7, 0, 3, 4, 5, 2, 1, 6 },     //Top half anticlockwise, bottom half clockwise
        new List<int>() { 7, 0, 5, 2, 3, 4, 1, 6 },     //Top half anticlockwise, bottom half anticlockwise
        new List<int>() { 6, 7, 4, 5, 2, 3, 0, 1 },     //Top and bottom X swap
        new List<int>() { 7, 5, 6, 4, 3, 1, 2, 0 },     //Top and bottom keys swap within their rows, middle X swaps
        new List<int>() { 3, 0, 1, 2, 5, 6, 7, 4 },     //Rows cycle down
        new List<int>() { 1, 2, 3, 0, 7, 4, 5, 6 },     //Rows cycle up
        new List<int>() { 0, 7, 6, 4, 5, 3, 2, 1 },     //Top-left stays, two diagonal swaps, bottom-right triplet cycles clockwise
        new List<int>() { 6, 5, 3, 4, 2, 1, 0, 7 },     //Top-right stays, two diagonal swaps, bottom-left triplet cycles clockwise
        new List<int>() { 6, 5, 4, 3, 2, 1, 7, 0 },     //Bottom-left stays, two diagonal swaps, top-right triplet cycles clockwise
        new List<int>() { 1, 7, 6, 5, 4, 3, 2, 0 }      //Bottom-right stays, two diagonal swaps, top-left triplet cycles clockwise
    };

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        Selectable.OnInteract += delegate { DisplayPress(); return false; };
        for (int i = 0; i < 8; i++)
        {
            InitKeyPositions.Add(Keys[i].transform.localPosition);
            Keys[i].color = Color.clear;
            Keys[i].transform.localPosition = Vector3.up * Keys[i].transform.localPosition.y;
        }
        Focus.color = Color.clear;
    }

    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void DisplayPress()
    {
        StartCoroutine(Intro());
        //SwapPos += 1;
        //SwapPos %= StandardSwaps.Count();
    }

    private IEnumerator Intro(float focusFadeInDur = 0.6f, float focusFlashDur = 0.9f, float keyFadeDur = 0.6f, 
        float intervalBetweenMvmts = 0.3f, float moveDur = 0.2f, 
        float greenInOutDur = 0.3f, float greenSustain = 0.5f)      //Intro must last 4.8s
    {
        if (Sound != null)
            Sound.StopSound();
        Sound = Audio.HandlePlaySoundAtTransformWithRef("music", transform);
        float timer = 0;
        while (timer < focusFadeInDur)
        {
            yield return null;
            timer += Time.deltaTime;
            Focus.color = new Color(1, 1, 1, Easing.OutSine(timer, 0, 1, focusFlashDur));
            Focus.transform.localScale = Vector3.one * Easing.OutExpo(timer, 0.025f, 0.03f, focusFadeInDur);
        }
        Focus.color = Color.white;
        Focus.transform.localScale = Vector3.one * 0.03f;
        timer = 0;
        while (timer < focusFlashDur)
        {
            yield return null;
            timer += Time.deltaTime;
            Focus.color = new Color(1, 1, 1, Easing.InQuint(timer, 1, 0, focusFlashDur));
        }
        Focus.color = Color.clear;
        timer = 0;
        while (timer < keyFadeDur)
        {
            yield return null;
            timer += Time.deltaTime;
            Keys[0].color = Color.Lerp(new Color(1, 0, 0, 0), Color.red, timer / keyFadeDur);
        }
        for (int i = 0; i < 8; i++)
            Keys[i].color = Color.red;
        for (int i = 0; i < 8; i++)
        {
            timer = 0;
            while (timer < moveDur)
            {
                yield return null;
                timer += Time.deltaTime;
                Keys[i].transform.localPosition = new Vector3(Easing.InOutSine(timer, 0, InitKeyPositions[i].x, moveDur),
                    Keys[i].transform.localPosition.y, Easing.InOutSine(timer, 0, InitKeyPositions[i].z, moveDur));
                Keys[i].transform.localEulerAngles = new Vector3(Keys[i].transform.localEulerAngles.x, Easing.OutSine(timer, 0, 360, moveDur), Keys[i].transform.localEulerAngles.z);
            }
            Keys[i].transform.localPosition = InitKeyPositions[i];
            Keys[i].transform.localEulerAngles = new Vector3(Keys[i].transform.localEulerAngles.x, 0, Keys[i].transform.localEulerAngles.z);
        }
        var CorrectKey = 0;
        timer = 0;
        while (timer < greenInOutDur)
        {
            yield return null;
            timer += Time.deltaTime;
            Keys[CorrectKey].color = Color.Lerp(Color.red, Color.green, timer / greenInOutDur);
        }
        Keys[CorrectKey].color = Color.green;
        timer = 0;
        while (timer < greenSustain)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        timer = 0;
        while (timer < greenInOutDur)
        {
            yield return null;
            timer += Time.deltaTime;
            Keys[CorrectKey].color = Color.Lerp(Color.green, Color.red, timer / greenInOutDur);
        }
        Keys[CorrectKey].color = Color.red;
    }

    private IEnumerator PerformSwap(List<int> newPositions, float duration = 0.25f)     //Duration of one beat = 0.3s
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            for (int i = 0; i < 8; i++)
                Keys[i].transform.localPosition = new Vector3(Easing.InOutSine(timer, InitKeyPositions[i].x, InitKeyPositions[newPositions.IndexOf(i)].x, duration), 0,
                    Easing.InOutSine(timer, InitKeyPositions[i].z, InitKeyPositions[newPositions.IndexOf(i)].z, duration));
        }
        for (int i = 0; i < 8; i++)
            Keys[i].transform.localPosition = InitKeyPositions[i];
    }
}
