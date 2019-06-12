using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;

public class ReorderedKeysScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo bomb;
    public KMColorblindMode ColorblindMode;

    public List<KMSelectable> keys;
    public Renderer[] meter;
    public Renderer[] keyID;
    public Material[] keyColours;

    private readonly int[][][] table = new int[3][][] {
        new int[6][]  { new int[6] { 2, 0, 5, 3, 1, 4},
                        new int[6] { 4, 3, 1, 2, 5, 0},
                        new int[6] { 5, 1, 2, 0, 4, 3},
                        new int[6] { 1, 2, 3, 4, 0, 5},
                        new int[6] { 0, 5, 4, 1, 3, 2},
                        new int[6] { 3, 4, 0, 5, 2, 1} },

        new int[6][]  { new int[6] { 1, 4, 0, 5, 3, 2},
                        new int[6] { 5, 3, 1, 2, 0, 4},
                        new int[6] { 3, 2, 5, 0, 4, 1},
                        new int[6] { 0, 5, 4, 1, 2, 3},
                        new int[6] { 4, 1, 2, 3, 5, 0},
                        new int[6] { 2, 0, 3, 4, 1, 5} },

        new int[6][]  { new int[6] { 5, 3, 6, 1, 2, 4},
                        new int[6] { 4, 1, 5, 3, 6, 2},
                        new int[6] { 1, 6, 2, 4, 5, 3},
                        new int[6] { 3, 5, 1, 2, 4, 6},
                        new int[6] { 2, 4, 3, 6, 1, 5},
                        new int[6] { 6, 2, 4, 5, 3, 1} } };

    private readonly string[] bad = new string[120] {"123465", "124635", "125346", "126453", "132564", "134265", "135624", "136542", "142356", "143625", "145236", "146523", "152436", "153642", "154632", "156243", "162345", "163524", "164352", "165432"
                                                    ,"213645", "214536", "215463", "216543", "231456", "234516", "235461", "236145", "241653", "243156", "245361", "246135", "251364", "253164", "254136", "256413", "261534", "263154", "264513", "265143"
                                                    ,"312546", "314652", "315246", "316254", "321654", "324615", "325614", "326451", "341256", "342651", "345261", "346251", "351264", "352641", "354126", "356241", "361245", "362541", "364125", "365214"
                                                    ,"412365", "413526", "415623", "416325", "421563", "423561", "425631", "426531", "431562", "432165", "435162", "436152", "451326", "452163", "453621", "456312", "461532", "462315", "463512", "465132"
                                                    ,"512634", "513462", "514362", "516423", "521634", "523146", "524316", "526314", "531426", "532416", "534612", "536412", "541263", "542361", "543216", "546123", "561342", "562431", "563124", "564213"
                                                    ,"612435", "613425", "614235", "615324", "621354", "623514", "624153", "625134", "631452", "632415", "634215", "635421", "641253", "642351", "643521", "645231", "651234", "652341", "653142", "654321"};
    private readonly string[] colourList = new string[6] { "Red", "Green", "Blue", "Cyan", "Magenta", "Yellow" };
    private int[][] info = new int[6][] { new int[4], new int[4], new int[4], new int[4], new int[4], new int[4] };
    private List<string> initialOrder = new List<string> { };
    private int stage = 1;
    private int pressCount;
    private int swapCount;
    private int resetCount;
    private IEnumerator[] sequence = new IEnumerator[2];
    private int[] keyCatch = new int[2];
    private bool pressable;
    private bool onepress;
    private int pivot;
    private bool[] alreadypressed = new bool[6] { true, true, true, true, true, true };
    private string answer;
    private List<string> labelList = new List<string> { };
    private bool colorblind;

    //Logging
    static int moduleCounter = 1;
    int moduleID;
    private bool moduleSolved;

    private void Awake()
    {
        moduleID = moduleCounter++;
        sequence[0] = Shuff();
        sequence[1] = Swap();
        foreach (Renderer m in meter)
        {
            m.material = keyColours[8];
        }
        foreach (KMSelectable key in keys)
        {
            key.OnInteract += delegate () { KeyPress(key); return false; };
            key.OnHighlight += delegate () { KeyHL(key); };
            key.OnHighlightEnded += delegate () { KeyHLEnd(key); };
        }
    }

    void Start()
    {
        colorblind = ColorblindMode.ColorblindModeActive;
        Reset();
    }

    private void KeyPress(KMSelectable key)
    {
        int k = keys.IndexOf(key);
        if (alreadypressed[k] == false && moduleSolved == false && pressable == true)
        {
            GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
            alreadypressed[k] = true;
            key.transform.localPosition = new Vector3(0, 0, -1f);
            if (k != pivot)
            {
                if (swapCount < 6)
                {
                    if (onepress == false)
                    {
                        onepress = true;
                        keyCatch[0] = k;
                    }
                    else
                    {
                        onepress = false;
                        pressable = false;
                        keyCatch[1] = k;
                        swapCount++;
                        Debug.LogFormat("[Reordered Keys #{0}] Swapped keys {1} and {2}: {3} swaps remaining", moduleID, keyCatch[0], keyCatch[1], 6 - swapCount);
                        StartCoroutine(sequence[1]);
                    }
                }
                else
                {
                    swapCount = 0;
                    Debug.LogFormat("[Reordered Keys #{0}] Out of swaps: Reset", moduleID);
                    resetCount++;
                    Reset();
                }
            }
            else
            {
                onepress = false;
                swapCount = 0;
                string[] IO = initialOrder.ToArray();
                string submission = String.Join(String.Empty, IO);
                if(submission == answer)
                {
                    Audio.PlaySoundAtTransform("InputCorrect", transform);
                    meter[stage - 1].material = keyColours[7];
                    if (stage < 2)
                    {
                        stage++;
                    }
                    else
                    {
                        moduleSolved = true;
                    }
                }
                else
                {
                    GetComponent<KMBombModule>().HandleStrike();
                }
                resetCount++;
                Reset();
            }
        }
    }

    private void KeyHL(KMSelectable key)
    {
        if (alreadypressed[keys.IndexOf(key)] == false && moduleSolved == false && pressable == true)
        {
            keyID[keys.IndexOf(key)].material = keyColours[8];
            key.GetComponentInChildren<TextMesh>().text = String.Empty;
        }
    }

    private void KeyHLEnd(KMSelectable key)
    {
        if (alreadypressed[keys.IndexOf(key)] == false && moduleSolved == false && pressable == true)
            setKey(keys.IndexOf(key));
    }

    private void setKey(int keyIndex)
    {
        keyID[keyIndex].material = keyColours[info[keyIndex][0]];
        switch (info[keyIndex][2])
        {
            case 0:
                keys[keyIndex].GetComponentInChildren<TextMesh>().color = new Color32(255, 25, 25, 255);
                break;
            case 1:
                keys[keyIndex].GetComponentInChildren<TextMesh>().color = new Color32(25, 255, 25, 255);
                break;
            case 2:
                keys[keyIndex].GetComponentInChildren<TextMesh>().color = new Color32(25, 25, 255, 255);
                break;
            case 3:
                keys[keyIndex].GetComponentInChildren<TextMesh>().color = new Color32(25, 255, 255, 255);
                break;
            case 4:
                keys[keyIndex].GetComponentInChildren<TextMesh>().color = new Color32(255, 75, 255, 255);
                break;
            default:
                keys[keyIndex].GetComponentInChildren<TextMesh>().color = new Color32(255, 255, 75, 255);
                break;
        }
        var label = (info[keyIndex][1] + 1).ToString();
        if (colorblind)
            label += "\n" + "RGBCMY"[info[keyIndex][2]] + "\n\n" + "RGBCMY"[info[keyIndex][0]];
        keys[keyIndex].GetComponentInChildren<TextMesh>().text = label;
    }

    private void Reset()
    {
        labelList.Clear();
        initialOrder.Clear();
        foreach (KMSelectable key in keys)
        {
            key.transform.localPosition = new Vector3(0, 0, -1f);
            key.GetComponentInChildren<TextMesh>().text = String.Empty;
            keyID[keys.IndexOf(key)].material = keyColours[8];
            alreadypressed[keys.IndexOf(key)] = false;
        }
        if (moduleSolved == false)
        {
            pivot = 0;
            pressable = false;
            List<int> initialList = new List<int> { 1, 2, 3, 4, 5, 6 };
            List<int> finalList = new List<int> { };
            for (int i = 0; i < 6; i++)
            {
                int temp = UnityEngine.Random.Range(0, initialList.Count());
                finalList.Add(initialList[temp]);
                initialOrder.Add(initialList[temp].ToString());
                initialList.RemoveAt(temp);
            }
            for (int i = 0; i < 6; i++)
            {
                info[i][0] = UnityEngine.Random.Range(0, 6);
                info[i][2] = UnityEngine.Random.Range(0, 6);
                info[i][3] = i + 1;
                int oh = table[1][info[i][2]][i];
                for (int j = 0; j < 6; j++)
                {
                    if (finalList[i] == table[2][j][oh])
                    {
                        info[i][1] = j;
                        break;
                    }
                }
                for (int j = 0; j < 6; j++)
                {
                    if (info[i][1] == table[0][info[i][0]][j])
                    {
                        info[i][1] = j;
                        break;
                    }
                }
                labelList.Add((info[i][1] + 1).ToString());
            }
            string[] a = new string[6];
            string[] b = new string[6];
            for (int i = 0; i < 6; i++)
            {
                a[i] = colourList[info[i][0]];
                b[i] = colourList[info[i][2]];
                if (i == 5)
                {
                    string A = String.Join(", ", a);
                    string B = String.Join(", ", b);
                    Debug.LogFormat("[Reordered Keys #{0}] After {1} reset(s), the buttons had the colours: {2}", moduleID, resetCount, A);
                    Debug.LogFormat("[Reordered Keys #{0}] After {1} reset(s), the labels had the colours: {2}", moduleID, resetCount, B);
                }
            }
            string[] label = labelList.ToArray();
            string[] IO = initialOrder.ToArray();
            string l = String.Join(", ", label);
            string order = String.Join(String.Empty, IO);
            Debug.LogFormat("[Reordered Keys #{0}] After {1} reset(s), the buttons were labelled: {2}", moduleID, resetCount, l);
            Debug.LogFormat("[Reordered Keys #{0}] After {1} reset(s), the initial order of key values was {2}", moduleID, resetCount, order);
            bool ech = false;
            for (int i = 0; i < 6; i++)
            {
                if(info[i][0] == info[i][2])
                {
                    if (ech == false)
                    {
                        pivot = finalList.IndexOf(info[i][1] + 1);
                        ech = true;
                    }
                    else
                    {
                        ech = false;
                        break;
                    }
                }
            }
            if (ech == false)
            {
                int pSum = 0;
                int sSum = 0;
                for(int i = 0; i < 6; i++)
                {
                    if(info[i][2] < 3)
                    {
                        pSum += info[i][1] + 1;
                    }
                    else
                    {
                        sSum += info[i][1] + 1;
                    }
                }
                pSum = pSum % 6;
                sSum = sSum % 6;
                pivot = finalList.IndexOf(table[2][pSum][sSum]);
            }
            Debug.LogFormat("[Reordered Keys #{0}] After {1} reset(s), the pivot key was {2}", moduleID, resetCount, pivot + 1);
            List<string> answ = new List<string> { "1", "2", "3", "4", "5", "6"};
            while(initialOrder[pivot] != answ[pivot])
            {
                string temp = answ[5];
                for(int i = 5; i > 0; i--)
                {
                    answ[i] = answ[i - 1];
                }
                answ[0] = temp;
            }
            string[] ans = answ.ToArray();
            answer = String.Join(String.Empty, ans);
            Debug.LogFormat("[Reordered Keys #{0}] After {1} reset(s), the desired order of key values was {2}", moduleID, resetCount, answer);
        }
        StartCoroutine(sequence[0]);
    }

    private IEnumerator Shuff()
    {
        for (int i = 0; i < 30; i++)
        {
            if (i % 5 == 4)
            {
                if (moduleSolved == true)
                {
                    alreadypressed[(i - 4) / 5] = false;
                    keyID[(i - 4) / 5].material = keyColours[6];
                    keys[(i - 4) / 5].GetComponentInChildren<TextMesh>().color = new Color32(0, 0, 0, 255);
                    keys[(i - 4) / 5].GetComponentInChildren<TextMesh>().text = "0";
                    if (i == 29)
                    {
                        GetComponent<KMBombModule>().HandlePass();
                        GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                    }
                }
                else
                {
                    alreadypressed[(i - 4) / 5] = false;
                    keys[(i - 4) / 5].transform.localPosition = new Vector3(0, 0, 0);
                    GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
                    setKey((i - 4) / 5);
                }
                if (i == 29)
                {
                    i = -1;
                    pressable = true;
                    StopCoroutine(sequence[0]);
                }
            }           
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator Swap()
    {
        for (int i = 0; i < 2; i++)
        {
            if(i == 1)
            {
                i = -1;
                pressable = true;
                int[] tempinfo = new int[3];
                tempinfo[0] = info[keyCatch[0]][0];
                tempinfo[1] = info[keyCatch[0]][1];
                tempinfo[2] = info[keyCatch[0]][2];
                string swapOrder = initialOrder[keyCatch[0]];
                foreach(KMSelectable key in keys)
                {
                    alreadypressed[keys.IndexOf(key)] = false;
                }

                keys[keyCatch[0]].transform.localPosition = new Vector3(0, 0, 0);
                keys[keyCatch[1]].transform.localPosition = new Vector3(0, 0, 0);
                info[keyCatch[0]][0] = info[keyCatch[1]][0];
                info[keyCatch[0]][1] = info[keyCatch[1]][1];
                info[keyCatch[0]][2] = info[keyCatch[1]][2];
                info[keyCatch[1]][0] = tempinfo[0];
                info[keyCatch[1]][1] = tempinfo[1];
                info[keyCatch[1]][2] = tempinfo[2];
                initialOrder[keyCatch[0]] = initialOrder[keyCatch[1]];
                initialOrder[keyCatch[1]] = swapOrder;
                string[] IO = initialOrder.ToArray();
                string order = String.Join(String.Empty, IO);
                if (bad.Contains(order))
                {
                    GetComponent<KMBombModule>().HandleStrike();
                }
                Debug.LogFormat("[Reordered Keys #{0}] After {1} reset(s) and {3} swaps, the order of key values were: {2}", moduleID, resetCount, order, swapCount);
                GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
                StopCoroutine(sequence[1]);
                setKey(keyCatch[0]);
                setKey(keyCatch[1]);
            }
            yield return new WaitForSeconds(1f);
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press 123456 [position in reading order]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^\s*(?:press\s*)?([123456 ,;]+)\s*$");
        if (!m.Success)
            yield break;

        foreach (var keyToPress in m.Groups[1].Value.Where(ch => ch >= '1' && ch <= '6').Select(ch => keys[ch - '1']))
        {
            yield return null;
            while (!pressable)
                yield return "trycancel";
            yield return new[] { keyToPress };
        }
    }
}
