using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Wawa.DDL;
using Wawa.Optionals;
using System.Text.RegularExpressions;
using Rnd = UnityEngine.Random;

public class LimboKeysScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBossModule Boss;
    public KMSelectable Selectable;
    public Sprite[] PossibleTexts;
    public SpriteRenderer[] Keys;
    public SpriteRenderer[] Glows;
    public SpriteRenderer Focus;
    public SpriteRenderer DecoyKey;
    public SpriteRenderer Eye;
    public SpriteRenderer SelectedText;
    public MeshRenderer Display;
    public MeshRenderer ModuleBG;
    public static string[] IgnoredModules = null;

    private Settings _Settings;

    class Settings
    {
        public bool MuteMusic = false;     //Forced false if FocusMode or BossMode are enabled
        public bool FocusMode = false;     //Forced true if BossMode is enabled
        public bool BossMode = false;

        /*
        
        MFB (possible configs)
        000
        010
        011
        100

        */
    }

    private bool MuteMusic, FocusMode, BossMode;

    void GetSettings()
    {
        var SettingsConfig = new ModConfig<Settings>("LimboKeys");
        _Settings = SettingsConfig.Settings; // This reads the settings from the file, or creates a new file if it does not exist
        SettingsConfig.Settings = _Settings; // This writes any updates or fixes if there's an issue with the file

        MuteMusic = _Settings.MuteMusic;
        FocusMode = _Settings.FocusMode;
        BossMode = _Settings.BossMode;

        if (BossMode)
        {
            FocusMode = true;
            Debug.LogFormat("[Limbo Keys #{0}] Focus mode has been enabled, as Boss mode is also enabled.", _moduleID);
        }
        if (FocusMode || BossMode)
        {
            MuteMusic = false;
            Debug.LogFormat("[Limbo Keys #{0}] Music has been enabled, as either Focus mode or Boss mode have been enabled.", _moduleID);
        }
        if (TwitchPlaysActive)  //Disable FocusMode on TP, as it cannot be done
        {
            FocusMode = false;
            Debug.LogFormat("[Limbo Keys #{0}] Focus mode has been disabled, as Twitch Plays is active.", _moduleID);
        }
    }

    void SortOutMissionDescription()
    {
        string description = Application.isEditor ? "[Limbo Keys] music off\n[Limbo Keys] focus off\n[Limbo Keys] boss on" : Missions.Description.UnwrapOr("");
        var matches = Regex.Matches(description, @"^(?:// )?\[Limbo ?Keys\] (.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (matches.Count == 0)
        {
            Debug.LogFormat("[Limbo Keys #{0}] Either nothing has been specified by the mission description, or this module is being played in Free Play.", _moduleID);
            GetSettings();
            return;
        }
        for (int i = 0; i < matches.Count; i++)
        {
            if (matches[i].Groups[1].Value.ToLowerInvariant() == "music on")
            {
                MuteMusic = false;
                Debug.LogFormat("[Limbo Keys #{0}] Music has been enabled, as specified by the mission description.", _moduleID);
            }
            if (matches[i].Groups[1].Value.ToLowerInvariant() == "music off")
            {
                MuteMusic = true;
                Debug.LogFormat("[Limbo Keys #{0}] Music has been disabled, as specified by the mission description.", _moduleID);
            }
            if (matches[i].Groups[1].Value.ToLowerInvariant() == "focus on")
            {
                FocusMode = true;
                Debug.LogFormat("[Limbo Keys #{0}] Focus mode has been enabled, as specified by the mission description.", _moduleID);
            }
            if (matches[i].Groups[1].Value.ToLowerInvariant() == "focus off")
            {
                FocusMode = false;
                Debug.LogFormat("[Limbo Keys #{0}] Focus mode has been disabled, as specified by the mission description.", _moduleID);
            }
            if (matches[i].Groups[1].Value.ToLowerInvariant() == "boss on")
            {
                BossMode = true;
                Debug.LogFormat("[Limbo Keys #{0}] Boss mode has been enabled, as specified by the mission description.", _moduleID);
            }
            if (matches[i].Groups[1].Value.ToLowerInvariant() == "boss off")
            {
                BossMode = false;
                Debug.LogFormat("[Limbo Keys #{0}] Boss mode has been disabled, as specified by the mission description.", _moduleID);
            }
        }
        if (BossMode)
        {
            FocusMode = true;
            Debug.LogFormat("[Limbo Keys #{0}] Focus mode has been enabled, as Boss mode is also enabled.", _moduleID);
        }
        if (FocusMode || BossMode)
        {
            MuteMusic = false;
            Debug.LogFormat("[Limbo Keys #{0}] Music has been enabled, as either Focus mode or Boss mode have been enabled.", _moduleID);
        }
        if (TwitchPlaysActive)  //Disable FocusMode on TP, as it cannot be done
        {
            FocusMode = false;
            Debug.LogFormat("[Limbo Keys #{0}] Focus mode has been disabled, as Twitch Plays is active.", _moduleID);
        }
    }

    private KMAudio.KMAudioRef Sound;
    private Coroutine KeyCycleAnim, KeyMovementAnim;
    private List<Vector3> InitKeyPositions = new List<Vector3>();
    private List<int> SwapIDs = new List<int>();
    private List<int> Colours = new List<int>();
    //private Color32[] ColoursForRends = new Color32[] { new Color32(244, 60, 87, 255), new Color32(255, 255, 132, 255), new Color32(210, 255, 109, 255), new Color32(135, 255, 187, 255), new Color32(168, 255, 253, 255), new Color32(90, 136, 255, 255), new Color32(181, 65, 255, 255), new Color32(254, 92, 255, 255) };
    private Color32[] ColoursForRends = new Color32[] { new Color32(255, 0, 0, 255), new Color32(255, 255, 20, 255), new Color32(20, 200, 10, 255), new Color32(50, 245, 255, 255), new Color32(0, 0, 255, 255), new Color32(155, 21, 99, 255), new Color32(255, 100, 255, 255), new Color32(255, 255, 255, 255) };
    private int CurrentSwap, DesiredKeyPos, Selected;
    private bool CannotPress, ReadyForSubmission, ReadyToMoveOn, Solved;

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

        if (IgnoredModules == null)
            IgnoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Limbo Keys", new string[]{ //Ignore full bosses
                "Limbo Keys",
                "Spectator Sport",
                "Sbemail Songs",
                "Temporal Sequence",
                "Hickory Dickory Dock",
                "Blackout",
                "Solve Shift",
                "FizzBoss",
                "Actions and Consequences",
                "Forgetle",
                "Reporting Anomalies",
                "Perspective Stacking",
                "In Order",
                "The Nobody’s Code",
                "OMISSION",
                "Piano Paradox",
                "Slight Gibberish Twist",
                "Pointer Pointer",
                "Forget Fractal",
                "Queen’s War",
                "Top 10 Numbers",
                "Bitwise Oblivion",
                "HyperForget",
                "Forget Me Maybe",
                "The Grand Prix",
                "Remembern’t Simple",
                "Remember Simple",
                "8",
                "ID Exchange",
                "Soulsong",
                "Forget Our Voices",
                "Twister",
                "Concentration",
                "Duck Konundrum",
                "The Board Walk",
                "Tetrahedron",
                "Cube Synchronization",
                "Soulscream",
                "+",
                "Forget Maze Not",
                "Black Arrows",
                "Floor Lights",
                "Shoddy Chess",
                "Security Council",
                "Keypad Directionality",
                "Forget Any Color",
                "Whiteout",
                "Busy Beaver",
                "A>N<D",
                "Kugelblitz",
                "OmegaForget",
                "Iconic",
                "The Twin",
                "RPS Judging",
                "Forget The Colors",
                "Brainf---",
                "Simon Forgets",
                "14",
                "Forget It Not",
                "Übermodule",
                "Forget Me Later",
                "Forget Perspective",
                "Organization",
                "Forget Us Not",
                "Forget Enigma",
                "Tallordered Keys",
                "Forget Them All",
                "Forget This",
                "Simon’s Stages",
                "Forget Everything",
                "Souvenir",
                "Forget Me Not"
            });

        Selectable.OnInteract += delegate { DisplayPress(); return false; };
        for (int i = 0; i < 8; i++)
        {
            InitKeyPositions.Add(Keys[i].transform.localPosition);
            Keys[i].color = Color.clear;
            Keys[i].transform.localPosition = Vector3.up * Keys[i].transform.localPosition.y;
            Glows[i].color = Color.clear;
        }
        Focus.color = Color.clear;
        SelectedText.gameObject.SetActive(false);
    }

    // Use this for initialization
    void Start()
    {
        Debug.LogFormat("[Limbo Keys #{0}] Welcome to Limbo... Let the games begin.", _moduleID);
        SortOutMissionDescription();
        if (FocusMode)
            ModuleBG.material.color = new Color(1 / 4f, 0, 0);

        if (!BossMode)
        {
            DecoyKey.gameObject.SetActive(true);
            Eye.gameObject.SetActive(false);
        }
        else
        {
            DecoyKey.gameObject.SetActive(false);
            Eye.gameObject.SetActive(true);
            Eye.color = Color.red;
            Selectable.transform.localScale = Vector3.zero;
            CannotPress = true;
            Module.OnActivate += delegate { StartCoroutine(WaitUntilEnoughSolves()); };
        }

        if (BossMode)
            Debug.LogFormat("[Limbo Keys #{0}] Boss mode is active.", _moduleID);
        if (FocusMode)
            Debug.LogFormat("[Limbo Keys #{0}] Focus mode is active.", _moduleID);
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
        Colours = Enumerable.Range(0, 8).ToList().Shuffle();
    }

    void DisplayPress()
    {
        Selectable.AddInteractionPunch();
        if (!ReadyForSubmission)
            StartCoroutine(Intro());
        else
            StartCoroutine(AfterSelection(Selected == DesiredKeyPos ? 0 : 1, Selected));
    }

    void Regen()
    {
        CurrentSwap = 0;
        DecoyKey.gameObject.SetActive(true);
        if (KeyCycleAnim != null)
            StopCoroutine(KeyCycleAnim);
        for (int i = 0; i < Keys.Length; i++)
        {
            Keys[i].transform.localPosition = Vector3.up * Keys[i].transform.localPosition.y;
            Keys[i].color = Glows[i].color = Color.clear;
        }
        Selectable.transform.localScale = new Vector3(0.75f, 0.001f, 0.75f);
        ReadyForSubmission = false;
        CannotPress = false;
    }

    private IEnumerator WaitUntilEnoughSolves(float redFadeDur = 4f, float keyFadeDur = 0.5f)
    {
        var required = Bomb.GetModuleNames().Where(x => !IgnoredModules.Contains(x)).Count();
        while (Bomb.GetSolvedModuleNames().Where(x => !IgnoredModules.Contains(x)).Count() < required)
            yield return new WaitForSeconds(0.1f);
        Debug.LogFormat("[Limbo Keys #{0}] The module is now ready to be solved.", _moduleID);
        if (!MuteMusic)
            Sound = Audio.HandlePlaySoundAtTransformWithRef("boss mode ready", transform, false);
        DecoyKey.gameObject.SetActive(true);
        var decoyInitColour = DecoyKey.color;
        DecoyKey.color = Color.clear;
        var eyeInit = Eye.color;
        float timer = 0;
        while (timer < redFadeDur)
        {
            yield return null;
            timer += Time.deltaTime;
            var eyeValue = Mathf.Max(Mathf.Lerp(1, -1, timer / redFadeDur), 0);
            Eye.color = eyeInit * new Color(eyeValue, eyeValue, eyeValue, Mathf.Min(Mathf.Lerp(2, 0, timer / redFadeDur), 1));
            Display.material.color = new Color(Mathf.PingPong(Mathf.Lerp(0, 2, timer / redFadeDur), 1), 0, 0);
        }
        Eye.color = new Color(1, 1, 0, 1);
        Eye.gameObject.SetActive(false);
        timer = 0;
        while (timer < keyFadeDur)
        {
            yield return null;
            timer += Time.deltaTime;
            DecoyKey.color = Color.Lerp(decoyInitColour * new Color(1, 1, 1, 0), decoyInitColour, timer / keyFadeDur);
        }
        DecoyKey.color = decoyInitColour;
        Display.material.color = Color.black;
        CannotPress = false;
        Selectable.transform.localScale = new Vector3(0.75f, 0.001f, 0.75f);
    }

    private IEnumerator AfterSelection(int state, int selected, float flickerMin = 0.3f, float flickerMax = 0.1f, float colourFlashDur = 0.6f, float eyeFadeInDur = 0.6f, float suspenseDur = 1.8f, float revealTextDur = 0.6f, float sustainDur = 0.6f)
    {
        ReadyForSubmission = false;
        CannotPress = true;
        Selectable.transform.localScale = Vector3.zero;
        if (KeyCycleAnim != null)
            StopCoroutine(KeyCycleAnim);
        var initKeyColours = new List<Color>();
        for (int i = 0; i < Keys.Length; i++)
            initKeyColours.Add(Keys[i].color);
        float timer = 0;
        if (selected > -1)
        {
            Display.material.color = ColoursForRends[selected];
            for (int i = 0; i < Keys.Length; i++)
                Keys[i].color = Glows[i].color = new Color(Glows[i].color.r, Glows[i].color.g, Glows[i].color.b, 0);
            while (timer < colourFlashDur)
            {
                yield return null;
                timer += Time.deltaTime;
                Display.material.color = Color.Lerp(ColoursForRends[selected], Color.black, timer / colourFlashDur);
            }
            Display.material.color = Color.black;
        }
        Eye.color = new Color(1, 1, 0, 0);
        Eye.gameObject.SetActive(true);
        timer = 0;
        while (timer < eyeFadeInDur)
        {
            yield return null;
            timer += Time.deltaTime;
            Eye.color = new Color(1, 1, 0, Mathf.Max(Mathf.Lerp(0, 1, timer / eyeFadeInDur) - Rnd.Range(flickerMin, flickerMax), 0));
            if (selected == -1)
                for (int i = 0; i < Keys.Length; i++)
                    Keys[i].color = Color.Lerp(initKeyColours[i], initKeyColours[i] * new Color(1, 1, 1, 0), timer / eyeFadeInDur);
        }
        for (int i = 0; i < Keys.Length; i++)
            Keys[i].color = initKeyColours[i] * new Color(1, 1, 1, 0);
        Eye.color = new Color(1, 1, 0, Mathf.Max(1 - Rnd.Range(flickerMin, flickerMax), 0));

        if (!FocusMode)
        {
            timer = 0;
            while (timer < suspenseDur)
            {
                yield return null;
                timer += Time.deltaTime;
                Eye.color = new Color(1, 1, 0, Mathf.Max(1 - Rnd.Range(flickerMin, flickerMax), 0));
            }
        }
        else
            while (!ReadyToMoveOn)
                yield return null;
        var targetColour = Color.red;
        if (state == 0)
            targetColour = Color.green;
        if (state == 2)
            targetColour = new Color(1, 1, 0, 1);
        SelectedText.sprite = PossibleTexts[state];
        SelectedText.color = new Color(1, 1, 0, 0);
        SelectedText.gameObject.SetActive(true);
        timer = 0;
        while (timer < revealTextDur)
        {
            yield return null;
            timer += Time.deltaTime;
            SelectedText.color = Color.Lerp(new Color(1, 1, 0, 0), targetColour, timer / revealTextDur);
            SelectedText.color = new Color(SelectedText.color.r, SelectedText.color.g, SelectedText.color.b, Mathf.Max(SelectedText.color.a - Rnd.Range(flickerMin, flickerMax), 0));
            Eye.color = Color.Lerp(new Color(1, 1, 0, 1), targetColour, timer / revealTextDur);
            Eye.color = new Color(Eye.color.r, Eye.color.g, Eye.color.b, Mathf.Max(1 - Rnd.Range(flickerMin, flickerMax), 0));
        }
        SelectedText.color = Eye.color = targetColour;
        SelectedText.color = new Color(SelectedText.color.r, SelectedText.color.g, SelectedText.color.b, Mathf.Max(1 - Rnd.Range(flickerMin, flickerMax), 0));
        Eye.color = new Color(Eye.color.r, Eye.color.g, Eye.color.b, Mathf.Max(1 - Rnd.Range(flickerMin, flickerMax), 0));
        timer = 0;
        while (timer < sustainDur)
        {
            yield return null;
            timer += Time.deltaTime;
            SelectedText.color = new Color(SelectedText.color.r, SelectedText.color.g, SelectedText.color.b, Mathf.Max(1 - Rnd.Range(flickerMin, flickerMax), 0));
            Eye.color = new Color(Eye.color.r, Eye.color.g, Eye.color.b, Mathf.Max(1 - Rnd.Range(flickerMin, flickerMax), 0));
        }
        if (state > 0 && BossMode)
        {
            Debug.LogFormat("[Limbo Keys #{0}] You chose the {1} key (the {2} key), which was incorrect. Death is inescapable...", _moduleID, new[] { "red", "yellow", "green", "cyan", "blue", "purple", "pink", "white" }[selected], new[] { "North", "North-West", "West", "South-West", "South", "South-East", "East", "North-East" }[selected]);
            while (true)
            {
                for (int i = 0; i < 10; i++)
                    Module.HandleStrike();
                timer = 0;
                while (timer < 1f)
                {
                    yield return null;
                    timer += Time.deltaTime;
                }
            }
        }
        if (state == 0)
        {
            Module.HandlePass();
            yield return "solve";
            Solved = true;
            Debug.LogFormat("[Limbo Keys #{0}] You chose the {1} key (the {2} key), which was correct. You have escaped... This time...", _moduleID, new[] { "red", "yellow", "green", "cyan", "blue", "purple", "pink", "white" }[selected], new[] { "North", "North-West", "West", "South-West", "South", "South-East", "East", "North-East" }[selected]);
            while (true)
            {
                yield return null;
                SelectedText.color = new Color(SelectedText.color.r, SelectedText.color.g, SelectedText.color.b, Mathf.Max(1 - Rnd.Range(flickerMin, flickerMax), 0));
                Eye.color = new Color(Eye.color.r, Eye.color.g, Eye.color.b, Mathf.Max(1 - Rnd.Range(flickerMin, flickerMax), 0));
            }
        }
        else if (state == 1)
        {
            Module.HandleStrike();
            yield return "strike";
            Debug.LogFormat("[Limbo Keys #{0}] You chose the {1} key (the {2} key), which was incorrect. Strike!", _moduleID, new[] { "red", "yellow", "green", "cyan", "blue", "purple", "pink", "white" }[selected], new[] { "North", "North-West", "West", "South-West", "South", "South-East", "East", "North-East" }[selected]);
            CurrentSwap = 0;
            StartCoroutine(HandleStrikeAnim(flickerMin, flickerMax, false));
        }
        else
        {
            Module.HandleStrike();
            yield return "strike";
            Debug.LogFormat("[Limbo Keys #{0}] Pick a key, coward! Don't let it win!", _moduleID);
            CurrentSwap = 0;
            StartCoroutine(HandleStrikeAnim(flickerMin, flickerMax, true));
        }
    }

    private IEnumerator HandleStrikeAnim(float flickerMin, float flickerMax, bool noSelection, float pauseDur = 1.5f, float fadeOutDur = 0.3f, float speenDur = 1.2f, float sustainDur = 0.06f, float retractDur = 0.3f)
    {
        float timer = 0;
        while (timer < pauseDur)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        timer = 0;
        while (timer < fadeOutDur)
        {
            yield return null;
            timer += Time.deltaTime;
            SelectedText.color = new Color(SelectedText.color.r, SelectedText.color.g, SelectedText.color.b, Mathf.Max(Mathf.Lerp(1, 0, timer / fadeOutDur) - Rnd.Range(flickerMin, flickerMax), 0));
            Eye.color = new Color(Eye.color.r, Eye.color.g, Eye.color.b, Mathf.Max(Mathf.Lerp(1, 0, timer / fadeOutDur) - Rnd.Range(flickerMin, flickerMax), 0));
        }
        SelectedText.gameObject.SetActive(false);
        Eye.gameObject.SetActive(false);
        timer = 0;
        while (timer < speenDur)
        {
            yield return null;
            timer += Time.deltaTime;
            for (int i = 0; i < Keys.Length; i++)
            {
                Keys[i].color = Color.Lerp(new Color(1, 0, 0, 0), Color.red, timer / speenDur);
                Keys[i].transform.localScale = Vector3.one * Easing.OutExpo(timer, 0, 0.025f, speenDur);
                Keys[i].transform.localPosition = PolarToCartesian(Easing.OutExpo(timer, (Mathf.PI / 4f) * i, (Mathf.PI / 4f) * (i + 2), speenDur)) * Easing.OutExpo(timer, 0, 0.06f, speenDur);
            }
        }
        for (int i = 0; i < Keys.Length; i++)
        {
            Keys[i].color = Color.red;
            Keys[i].transform.localScale = Vector3.one * 0.025f;
            Keys[i].transform.localPosition = PolarToCartesian((Mathf.PI / 4f) * (i + 2)) * 0.06f;
        }
        timer = 0;
        while (timer < sustainDur)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        timer = 0;
        while (timer < retractDur)
        {
            yield return null;
            timer += Time.deltaTime;
            for (int i = 0; i < Keys.Length; i++)
                Keys[i].transform.localPosition = PolarToCartesian((Mathf.PI / 4f) * (i + 2)) * Easing.InSine(timer, 0.06f, 0, retractDur);
        }
        for (int i = 0; i < Keys.Length; i++)
        {
            Keys[i].transform.localPosition = Vector3.up * Keys[i].transform.localPosition.y;
            Keys[i].color = Color.clear;
        }
        ReadyToMoveOn = false;
        DecoyKey.gameObject.SetActive(true);
        Selectable.transform.localScale = new Vector3(0.75f, 0.001f, 0.75f);
        CannotPress = false;
    }

    private IEnumerator Intro(float focusFadeInDur = 0.5f, float focusFlashDur = 0.9f, float keyFadeDur = 0.6f,
        float moveDur = 0.2f, float greenInOutDur = 0.3f, float greenSustain = 0.4f)                                     //Intro must last 4.8s
    {
        CannotPress = true;
        Selectable.transform.localScale = Vector3.zero;
        GenerateSwaps();
        if (Sound != null)
            Sound.StopSound();
        if (!MuteMusic)
        {
            if (Sound != null)
                Sound.StopSound();
            Sound = Audio.HandlePlaySoundAtTransformWithRef("music", transform, false);
        }
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
        var initGlowColour = Color.green * new Color(1, 1, 1, 0);
        var finalGlowColour = Color.green * new Color(1, 1, 1, 1 / 3f);
        timer = 0;
        while (timer < greenInOutDur)
        {
            yield return null;
            timer += Time.deltaTime;
            Keys[DesiredKeyPos].color = Color.Lerp(Color.red, Color.green, timer / greenInOutDur);
            Glows[DesiredKeyPos].color = Color.Lerp(initGlowColour, finalGlowColour, timer / greenInOutDur);
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
            Glows[DesiredKeyPos].color = Color.Lerp(finalGlowColour, initGlowColour, timer / greenInOutDur);
        }
        Keys[DesiredKeyPos].color = Color.red;
        Glows[DesiredKeyPos].color = initGlowColour;
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
        }
        swaps.Add(TopWithBottomHalf());
        intervals.Add(0.6f);
        DesiredKeyPos = Array.IndexOf(new[] { 2, 3, 0, 1, 6, 7, 4, 5 }, DesiredKeyPos);
        for (int i = 5; i < 8; i++)
        {
            swaps.Add(PerformSwap(StandardSwaps[SwapIDs[i]]));
            intervals.Add(0.3f);
            DesiredKeyPos = StandardSwaps[SwapIDs[i]].IndexOf(DesiredKeyPos);
        }
        swaps.Add(PerformRevolution(true));
        intervals.Add(0.6f);
        DesiredKeyPos = Array.IndexOf(new[] { 4, 5, 6, 7, 0, 1, 2, 3 }, DesiredKeyPos);
        for (int i = 8; i < 16; i++)
        {
            swaps.Add(PerformSwap(StandardSwaps[SwapIDs[i]]));
            intervals.Add(0.3f);
            DesiredKeyPos = StandardSwaps[SwapIDs[i]].IndexOf(DesiredKeyPos);
        }
        swaps.Add(PerformRevolution(false));
        intervals.Add(0.6f);
        DesiredKeyPos = Array.IndexOf(new[] { 4, 5, 6, 7, 0, 1, 2, 3 }, DesiredKeyPos);
        for (int i = 16; i < 22; i++)
        {
            swaps.Add(PerformSwap(StandardSwaps[SwapIDs[i]]));
            intervals.Add(0.3f);
            DesiredKeyPos = StandardSwaps[SwapIDs[i]].IndexOf(DesiredKeyPos);
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
        Debug.LogFormat("[Limbo Keys #{0}] The correct key is the {1} key (the {2} key).", _moduleID, new[] { "red", "yellow", "green", "cyan", "blue", "purple", "pink", "white" }[DesiredKeyPos], new[] { "North", "North-West", "West", "South-West", "South", "South-East", "East", "North-East" }[DesiredKeyPos]);
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
            KeyCycleAnim = StartCoroutine(DoFlashes());
    }

    private IEnumerator DoFlashes(float sustain = 0.5f, float glowAlpha = 1 / 2f)
    {
        CannotPress = false;
        ReadyForSubmission = true;
        Selectable.transform.localScale = new Vector3(0.75f, 0.001f, 0.75f);
        var order = Enumerable.Range(0, 8).ToList();
        var i = Selected = 0;
        if (FocusMode)
            StartCoroutine(BlowWhistleWhenReady((sustain * 8) + 1.2f));
        do
        {
            Keys[order[i]].color = new Color(Keys[order[i]].color.r, Keys[order[i]].color.g, Keys[order[i]].color.b, 1);
            Glows[order[i]].color = new Color(Glows[order[i]].color.r, Glows[order[i]].color.g, Glows[order[i]].color.b, glowAlpha);
            float timer = 0;
            while (timer < sustain)
            {
                yield return null;
                timer += Time.deltaTime;
                Keys[order[i]].color = new Color(Keys[order[i]].color.r, Keys[order[i]].color.g, Keys[order[i]].color.b, Mathf.Lerp(1, 1 / 2f, timer / sustain));
                Glows[order[i]].color = new Color(Glows[order[i]].color.r, Glows[order[i]].color.g, Glows[order[i]].color.b, Mathf.Lerp(glowAlpha, 0, timer / sustain));
            }
            Keys[order[i]].color = new Color(Keys[order[i]].color.r, Keys[order[i]].color.g, Keys[order[i]].color.b, 1 / 2f);
            Glows[order[i]].color = new Color(Glows[order[i]].color.r, Glows[order[i]].color.g, Glows[order[i]].color.b, 0);
            i = (i + 1) % Keys.Length;
            Selected = i;
        }
        while (!FocusMode || i != 0);
        CannotPress = true;
        ReadyForSubmission = false;
        Selectable.transform.localScale = Vector3.zero;
        StartCoroutine(AfterSelection(2, -1));
    }

    private IEnumerator BlowWhistleWhenReady(float duration)
    {
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        ReadyToMoveOn = true;
    }

    private bool TwitchPlaysActive = false;

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} go' to initialise the module. Use '!{0} [colour or cardinal direction]' to select a key. Colours are red, yellow, green, cyan, blue, purple, pink, white, and cardinal directions are North, North-West, West, South-West, South, South-East, East, North-East. These can be abbreviated as R, Y, G, C, B, P, I, W and No, NW, We, SW, So, SE, Ea, NE. If something caused the stream to lag, making the series of key swaps impossible to see, use '!{0} regen' to regenerate the keys, with no penalty (can only be used when the keys are cycling).";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant().Replace(" ", "-");
        if (command == "go")
        {
            if (!CannotPress && !ReadyForSubmission)    //i.e. the module is yet to be initialised
            {
                yield return null;
                Selectable.OnInteract();
            }
            else
            {
                yield return "sendtochaterror Cannot initialise the module: the module has already been initialised!";
                yield break;
            }
        }
        else if (command == "regen")
        {
            if (!CannotPress && ReadyForSubmission)
            {
                yield return null;
                Regen();
            }
            else
                yield return "sendtochaterror Cannot regenerate the keys: the module is not yet ready for submission!";
            yield break;
        }
        else
        {
            var validColoursFull = new[] { "red", "yellow", "green", "cyan", "blue", "purple", "pink", "white" };
            var validColoursAbbr = new[] { "r", "y", "g", "c", "b", "p", "i", "w" };
            var validCardinalsFull = new[] { "north", "north-west", "west", "south-west", "south", "south-east", "east", "north-east" };
            var validCardinalsAbbr = new[] { "no", "nw", "we", "sw", "so", "se", "ea", "ne" };
            if (validColoursFull.Contains(command) || validColoursAbbr.Contains(command) || validCardinalsFull.Contains(command) || validCardinalsAbbr.Contains(command))
            {
                if (!CannotPress && ReadyForSubmission)
                {
                    var ix = Mathf.Max(Array.IndexOf(validColoursFull, command), Array.IndexOf(validColoursAbbr, command), Array.IndexOf(validCardinalsFull, command), Array.IndexOf(validCardinalsAbbr, command));
                    yield return null;
                    while (Selected != ix)
                        yield return null;
                    if (Selected == ix)  //Just to make sure
                        Selectable.OnInteract();
                }
                else
                {
                    yield return "sendtochaterror Cannot select that key: the keys are not currently cycling!";
                    yield break;
                }
            }
            else
            {
                yield return "sendtochaterror Invalid command.";
                yield break;
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!Solved)
        {
            if (CannotPress)
                yield return true;
            else
            {
                if (!ReadyForSubmission)
                {
                    yield return null;
                    Selectable.OnInteract();
                }
                else
                {
                    yield return null;
                    while (Selected != DesiredKeyPos)
                        yield return null;
                    if (Selected == DesiredKeyPos)  //Just to make sure
                        Selectable.OnInteract();
                }
            }
        }
    }
}