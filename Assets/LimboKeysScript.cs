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
    public SpriteRenderer[] Glows;
    public SpriteRenderer Focus;
    public SpriteRenderer DecoyKey;

    private KMAudio.KMAudioRef Sound;
    private Coroutine KeyMovementAnim;
    private List<Vector3> InitKeyPositions = new List<Vector3>();
    private List<int> SwapIDs = new List<int>();
    private List<int> Colours = new List<int>();
    //private Color32[] ColoursForRends = new Color32[] { new Color32(244, 60, 87, 255), new Color32(255, 255, 132, 255), new Color32(210, 255, 109, 255), new Color32(135, 255, 187, 255), new Color32(168, 255, 253, 255), new Color32(90, 136, 255, 255), new Color32(181, 65, 255, 255), new Color32(254, 92, 255, 255) };
    private Color32[] ColoursForRends = new Color32[] { new Color32(255, 0, 0, 255), new Color32(255, 255, 20, 255), new Color32(20, 200, 10, 255), new Color32(50, 245, 255, 255), new Color32(0, 0, 255, 255), new Color32(155, 21, 99, 255), new Color32(255, 100, 255, 255), new Color32(255, 255, 255, 255) };
    private int CurrentSwap, DesiredKeyPos, Selected;
    private bool CannotPress;

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
        new List<int>() { 4, 0, 1, 2, 5, 6, 7, 3 },     //Rows cycle down, the row moving the most swaps
        new List<int>() { 1, 2, 3, 7, 0, 4, 5, 6 },     //Rows cycle up, the row moving the most swaps
        new List<int>() { 0, 7, 6, 4, 5, 3, 2, 1 },     //Top-left stays, two diagonal swaps, bottom-right triplet cycles clockwise
        new List<int>() { 6, 5, 3, 4, 2, 1, 0, 7 },     //Top-right stays, two diagonal swaps, bottom-left triplet cycles clockwise
        new List<int>() { 6, 5, 4, 3, 2, 1, 7, 0 },     //Bottom-left stays, two diagonal swaps, top-right triplet cycles clockwise
        new List<int>() { 1, 7, 6, 5, 4, 3, 2, 0 },     //Bottom-right stays, two diagonal swaps, top-left triplet cycles clockwise
        new List<int>() { 6, 5, 3, 4, 2, 1, 7, 0 },     //One diagonal swap, top-right and bottom-left triplets cycle clockwise
        new List<int>() { 7, 5, 4, 2, 3, 1, 0, 6 },     //One diagonal swap, top-right and bottom-left triplets cycle anticlockwise
        new List<int>() { 1, 7, 6, 4, 5, 3, 2, 0 },     //One diagonal swap, top-left and bottom-right triplets cycle clockwise
        new List<int>() { 7, 0, 6, 5, 3, 4, 2, 1 },     //One diagonal swap, top-left and bottom-right triplets cycle anticlockwise
        new List<int>() { 6, 2, 3, 4, 5, 1, 7, 0 },     //Jigsaw swap, triplet at top-right
        new List<int>() { 7, 0, 6, 2, 3, 4, 5, 1 },     //Jigsaw swap, triplet at top-left
        new List<int>() { 1, 5, 3, 4, 2, 6, 7, 0 },     //Jigsaw swap, triplet at bottom-left
        new List<int>() { 7, 0, 1, 5, 3, 4, 2, 6 },     //Jigsaw swap, triplet at bottom-right
    };

    private Vector3 PolarToCartesian(float angle) //Assumed radius 1, angle taken in radians from the +ve x-axis, anticlockwise
    {
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
    }

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        Selectable.OnInteract += delegate { DisplayPress(); return false; };
        for (int i = 0; i < 8; i++)
        {
            InitKeyPositions.Add(Keys[i].transform.localPosition);
            Keys[i].color = Color.clear;
            Keys[i].transform.localPosition = Vector3.up * Keys[i].transform.localPosition.y;
            Glows[i].color = Color.clear;
        }
        Focus.color = Color.clear;
        DecoyKey.gameObject.SetActive(true);
    }

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    void GenerateSwaps()
    {
        DesiredKeyPos = Rnd.Range(0, 8);
        SwapIDs = new List<int>();
        for (int i = 0; i < 25 - 3; i++) //For the three special rotations
            SwapIDs.Add(Rnd.Range(0, StandardSwaps.Count()));
        Debug.Log(SwapIDs.Join(", "));
        Colours = Enumerable.Range(0, 8).ToList().Shuffle();
    }

    void DisplayPress()
    {
        StartCoroutine(Intro());
    }

    private IEnumerator Intro(float focusFadeInDur = 0.5f, float focusFlashDur = 0.9f, float keyFadeDur = 0.6f,
        float moveDur = 0.2f, float greenInOutDur = 0.3f, float greenSustain = 0.4f)                                     //Intro must last 4.8s
    {
        Selectable.transform.localScale = Vector3.zero;
        GenerateSwaps();
        if (Sound != null)
            Sound.StopSound();
        Sound = Audio.HandlePlaySoundAtTransformWithRef("music", transform, false);
        float timer = 0;
        while (timer < focusFadeInDur)
        {
            yield return null;
            timer += Time.deltaTime;
            Focus.color = new Color(1, 1, 1, Easing.OutSine(timer, 0, 1, focusFlashDur));
            Focus.transform.localScale = Vector3.one * Easing.OutExpo(timer, 0.025f, 0.03f, focusFadeInDur);
            DecoyKey.color = new Color(1, 0, 0, Easing.OutExpo(timer, 1, 0, focusFlashDur));
            DecoyKey.transform.localScale = Vector3.one * Easing.OutExpo(timer, 0.0225f, 0, focusFadeInDur);
        }
        Focus.color = Color.white;
        Focus.transform.localScale = Vector3.one * 0.03f;
        DecoyKey.color = Color.red;
        DecoyKey.transform.localScale = Vector3.one * 0.0225f;
        DecoyKey.gameObject.SetActive(false);
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
        timer = 0;
        while (timer < greenInOutDur)
        {
            yield return null;
            timer += Time.deltaTime;
            Keys[DesiredKeyPos].color = Color.Lerp(Color.red, Color.green, timer / greenInOutDur);
        }
        Keys[DesiredKeyPos].color = Color.green;
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
            Keys[DesiredKeyPos].color = Color.Lerp(Color.green, Color.red, timer / greenInOutDur);
        }
        Keys[DesiredKeyPos].color = Color.red;
        StartCoroutine(SwapSequence());
    }

    private IEnumerator PerformSwap(List<int> newPositions, float duration = 0.25f)     //Duration of one beat = 0.3s
    {
        for (int i = 0; i < 8; i++)
        {
            Keys[i].transform.localPosition = InitKeyPositions[i];
            Keys[i].transform.localEulerAngles = new Vector3(Keys[i].transform.localEulerAngles.x, CurrentSwap > 9 && CurrentSwap < 18 ? 180 : 0, Keys[i].transform.localEulerAngles.z);
        }
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

    private IEnumerator TopWithBottomHalf(float duration = 0.55f, float movementOut = 0.015f)
    {
        for (int i = 0; i < 8; i++)
        {
            Keys[i].transform.localPosition = InitKeyPositions[i];
            Keys[i].transform.localEulerAngles = new Vector3(Keys[i].transform.localEulerAngles.x, 0, Keys[i].transform.localEulerAngles.z);
        }

        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            foreach (var i in new[] { 0, 1, 6, 7 })
                Keys[i].transform.localPosition = new Vector3(InitKeyPositions[i].x + (Mathf.Sin((timer / duration) * Mathf.PI) * movementOut), 0,
                    Easing.InOutSine(timer, InitKeyPositions[i].z, InitKeyPositions[i].z - 0.08f, duration));
            foreach (var i in new[] { 2, 3, 4, 5 })
                Keys[i].transform.localPosition = new Vector3(InitKeyPositions[i].x - (Mathf.Sin((timer / duration) * Mathf.PI) * movementOut), 0,
                    Easing.InOutSine(timer, InitKeyPositions[i].z, InitKeyPositions[i].z + 0.08f, duration));
        }
        for (int i = 0; i < 8; i++)
            Keys[i].transform.localPosition = InitKeyPositions[i];
    }

    private IEnumerator PerformRevolution(bool isClock, float duration = 0.55f)     //Duration of one beat = 0.3s
    {
        for (int i = 0; i < 8; i++)
        {
            Keys[i].transform.localPosition = InitKeyPositions[i];
            Keys[i].transform.localEulerAngles = new Vector3(Keys[i].transform.localEulerAngles.x, isClock ? 0 : 180, Keys[i].transform.localEulerAngles.z);
        }
        //Gotta love polar coordinates! :D
        var radii = new List<float>();
        for (int i = 0; i < 8; i++)
            radii.Add(Mathf.Sqrt(Mathf.Pow(Keys[i].transform.localPosition.x, 2) + Mathf.Pow(Keys[i].transform.localPosition.z + 0.02f, 2)));
        var initAngles = new List<float>();
        for (int i = 0; i < 8; i++)
            initAngles.Add(Mathf.Atan((Keys[i].transform.localPosition.z + 0.02f) / Keys[i].transform.localPosition.x) + (i < 4 ? Mathf.PI : 0));
        float timer = isClock ? 0 : duration;
        while (isClock ? (timer < duration) : timer > 0)
        {
            yield return null;
            timer += isClock ? Time.deltaTime : -Time.deltaTime;
            for (int i = 0; i < 2; i++)
                Keys[i * 7].transform.localPosition = new Vector3(Easing.InOutSine(timer, InitKeyPositions[i * 7].x, InitKeyPositions[4 - i].x, duration), 0,        //4 - i: i = 0, 7 => i = 4, 3
                    Easing.InOutSine(timer, InitKeyPositions[i * 7].z, InitKeyPositions[4 - i].z, duration));
            for (int i = 0; i < 8; i++)
                if (i != 0 && i != 7)
                    Keys[i].transform.localPosition = new Vector3(radii[i] * Mathf.Cos(Easing.InOutSine(timer, initAngles[i], initAngles[i] - Mathf.PI, duration)), 0,
                        Easing.InOutSine(timer, -0.02f, 0.02f, duration)
                            + (radii[i] * Mathf.Sin(Easing.InOutSine(timer, initAngles[i], initAngles[i] - Mathf.PI, duration))));
            for (int i = 0; i < 8; i++)
                Keys[i].transform.localEulerAngles = new Vector3(Keys[i].transform.localEulerAngles.x, Easing.InOutSine(timer, 0, 180, duration), Keys[i].transform.localEulerAngles.z);
        }
        for (int i = 0; i < 8; i++)
        {
            Keys[i].transform.localPosition = InitKeyPositions[i];
            Keys[i].transform.localEulerAngles = new Vector3(Keys[i].transform.localEulerAngles.x, isClock ? 180 : 0, Keys[i].transform.localEulerAngles.z);
        }
    }

    private IEnumerator SwapSequence()
    {
        var swaps = new List<IEnumerator>();
        var intervals = new List<float>();
        for (int i = 0; i < 5; i++)
        {
            swaps.Add(PerformSwap(StandardSwaps[SwapIDs[i]]));
            intervals.Add(0.3f);
            DesiredKeyPos = StandardSwaps[SwapIDs[i]].IndexOf(DesiredKeyPos);
            Debug.Log(DesiredKeyPos);
        }
        swaps.Add(TopWithBottomHalf());
        intervals.Add(0.6f);
        DesiredKeyPos = Array.IndexOf(new[] { 2, 3, 0, 1, 6, 7, 4, 5 }, DesiredKeyPos);
        Debug.Log(DesiredKeyPos);
        for (int i = 5; i < 8; i++)
        {
            swaps.Add(PerformSwap(StandardSwaps[SwapIDs[i]]));
            intervals.Add(0.3f);
            DesiredKeyPos = StandardSwaps[SwapIDs[i]].IndexOf(DesiredKeyPos);
            Debug.Log(DesiredKeyPos);
        }
        swaps.Add(PerformRevolution(true));
        intervals.Add(0.6f);
        DesiredKeyPos = Array.IndexOf(new[] { 4, 5, 6, 7, 0, 1, 2, 3 }, DesiredKeyPos);
        Debug.Log(DesiredKeyPos);
        for (int i = 8; i < 16; i++)
        {
            swaps.Add(PerformSwap(StandardSwaps[SwapIDs[i]]));
            intervals.Add(0.3f);
            DesiredKeyPos = StandardSwaps[SwapIDs[i]].IndexOf(DesiredKeyPos);
            Debug.Log(DesiredKeyPos);
        }
        swaps.Add(PerformRevolution(false));
        intervals.Add(0.6f);
        DesiredKeyPos = Array.IndexOf(new[] { 4, 5, 6, 7, 0, 1, 2, 3 }, DesiredKeyPos);
        Debug.Log(DesiredKeyPos);
        for (int i = 16; i < 22; i++)
        {
            swaps.Add(PerformSwap(StandardSwaps[SwapIDs[i]]));
            intervals.Add(0.3f);
            DesiredKeyPos = StandardSwaps[SwapIDs[i]].IndexOf(DesiredKeyPos);
            Debug.Log(DesiredKeyPos);
        }
        foreach (var swap in swaps)
        {
            if (KeyMovementAnim != null)
                StopCoroutine(KeyMovementAnim);
            KeyMovementAnim = StartCoroutine(swap);
            float timer = 0;
            while (timer < intervals.First())
            {
                yield return null;
                timer += Time.deltaTime;
            }
            intervals.RemoveAt(0);
            CurrentSwap++;
        }
        StartCoroutine(FinishSwaps());
    }

    private IEnumerator FinishSwaps(float interval = 0.0625f)
    {
        var order = new[] { 7, 6, 0, 5, 1, 4, 2, 3 };
        for (int i = 0; i < 8; i++)
        {
            Keys[i].transform.localPosition = InitKeyPositions[i];
            Keys[i].transform.localEulerAngles = new Vector3(Keys[i].transform.localEulerAngles.x, 0, Keys[i].transform.localEulerAngles.z);
        }
        for (int i = 0; i < Keys.Length; i++)
        {
            StartCoroutine(SpinKey(order[i]));
            StartCoroutine(MoveKey(order[i], i == Keys.Length - 1));
            float timer = 0;
            while (timer < interval)
            {
                yield return null;
                timer += Time.deltaTime;
            }
        }
    }

    private IEnumerator SpinKey(int pos, float duration = 0.75f, byte glowAlpha = 64)
    {
        var initColour = Keys[pos].color;
        var initGlowColour = new Color32(ColoursForRends[pos].r, ColoursForRends[pos].g, ColoursForRends[pos].b, 0);
        var finalGlowColour = new Color32(ColoursForRends[pos].r, ColoursForRends[pos].g, ColoursForRends[pos].b, glowAlpha);
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Keys[pos].transform.localEulerAngles = new Vector3(Keys[pos].transform.localEulerAngles.x, Easing.OutExpo(timer, 0, 360, duration), Keys[pos].transform.localEulerAngles.z);
            Keys[pos].color = Color32.Lerp(initColour, ColoursForRends[pos], timer / duration);
            Glows[pos].color = Color32.Lerp(initGlowColour, finalGlowColour, timer / duration);
        }
        Keys[pos].transform.localEulerAngles = new Vector3(Keys[pos].transform.localEulerAngles.x, 0, Keys[pos].transform.localEulerAngles.z);
        Keys[pos].color = ColoursForRends[pos];
        Glows[pos].color = finalGlowColour;
    }

    private IEnumerator MoveKey(int pos, bool triggerFlashes, float duration = 0.75f, byte glowAlpha = 64, byte keyAlpha = 128)
    {
        var init = Keys[pos].transform.localPosition;
        var target = PolarToCartesian(new[] { 2, 3, 4, 5, 6, 7, 0, 1 }[pos] * (Mathf.PI / 4)) * 0.06f;
        var initGlowColour = new Color32(ColoursForRends[pos].r, ColoursForRends[pos].g, ColoursForRends[pos].b, glowAlpha);
        var finalGlowColour = new Color32(ColoursForRends[pos].r, ColoursForRends[pos].g, ColoursForRends[pos].b, 0);
        var finalKeyColour = new Color32(ColoursForRends[pos].r, ColoursForRends[pos].g, ColoursForRends[pos].b, keyAlpha);
        float timer = 0;
        while (timer < 0.8f)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            Keys[pos].transform.localPosition = new Vector3(Easing.InOutQuad(timer, init.x, target.x, duration), 0, Easing.InOutQuad(timer, init.z, target.z, duration));
            Keys[pos].color = Color32.Lerp(ColoursForRends[pos], finalKeyColour, timer / duration);
            Glows[pos].color = Color32.Lerp(initGlowColour, finalGlowColour, timer / duration);
        }
        Keys[pos].transform.localPosition = target;
        Keys[pos].color = finalKeyColour;
        Glows[pos].color = finalGlowColour;
        if (triggerFlashes)
            StartCoroutine(DoFlashes());
    }

    private IEnumerator DoFlashes(float sustain = 0.60f, float glowAlpha = 1 / 2f)
    {
        Selectable.transform.localScale = new Vector3(0.75f, 0.001f, 0.75f);
        var order = Enumerable.Range(0, 8).ToList();
        var i = Selected = 0;
        while (true)
        {
            Keys[order[i]].color = new Color(Keys[order[i]].color.r, Keys[order[i]].color.g, Keys[order[i]].color.b, 1);
            Glows[order[i]].color = new Color(Glows[order[i]].color.r, Glows[order[i]].color.g, Glows[order[i]].color.b, glowAlpha);
            float timer = 0;
            while (timer < sustain)
            {
                yield return null;
                timer += Time.deltaTime;
                Keys[order[i]].color = new Color(Keys[order[i]].color.r, Keys[order[i]].color.g, Keys[order[i]].color.b, Mathf.Lerp(1, 1/2f, timer / sustain));
                Glows[order[i]].color = new Color(Glows[order[i]].color.r, Glows[order[i]].color.g, Glows[order[i]].color.b, Mathf.Lerp(glowAlpha, 0, timer / sustain));
            }
            Keys[order[i]].color = new Color(Keys[order[i]].color.r, Keys[order[i]].color.g, Keys[order[i]].color.b, 1/2f);
            Glows[order[i]].color = new Color(Glows[order[i]].color.r, Glows[order[i]].color.g, Glows[order[i]].color.b, 0);
            i = (i + 1) % Keys.Length;
            Selected = (Selected + 1) % Keys.Length;
        }
    }
}