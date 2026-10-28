using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    [Header("Excel")]
    [Tooltip("Tick for Danish/European Excel (decimal comma). Untick for English Excel.")]
    [SerializeField] private bool decimalComma = true;

    private const string ParticipantsFile = "participants.csv";
    private const string SeriesFile = "timeseries.csv";
    private const string QuizFile = "quiz_answers.csv";
    private const char Sep = ';';
    private const float FlushInterval = 10f;          // append time-series rows
    private const float SaveInterval = 30f;           // rewrite participants.csv
    private const float TemperatureSettleTime = 0.5f; // no change for 0.5 s = a "setting"
    private const string CounterKey = "telemetry_next_participant";
    private float quizPageShownAt;

    private static readonly string[] CoreColumns =
    {
        "ParticipantID", "NarrationLanguage", "SessionStartUTC",
        "SessionEndUTC", "SessionDurationSec",
        "Ending", "EndingReachedSec",
        "Temp_Start"
    };

    private static readonly string[] SeriesHeader =
        { "ParticipantID", "TimeSec", "Type", "Value", "Source", "DurationSec" };
    private static readonly string[] QuizHeader =
        { "ParticipantID", "QuestionID", "Answer", "OptionCorrect", "QuestionCorrect", "TimeSec" };

    private class OpenEntry
    {
        public float time;
        public string value;
        public float number;
        public string source;
    }

    private readonly Dictionary<string, string> fields = new();
    private readonly List<string> schema = new();
    private readonly Dictionary<string, OpenEntry> open = new();
    private readonly List<string> pendingLines = new();
    private readonly SortedDictionary<int, float> tempTotals = new();
    private readonly Dictionary<int, float> choiceAvailableAt = new();
    private readonly Dictionary<string, float> quizShownAt = new();

    private float sessionStartRealtime;
    private float sessionEndTime;
    private float nextFlush;
    private float nextSave;
    private int dilemmaCounter;

    // Temperature debounce
    private bool tempPending;
    private float tempPendingValue;
    private float tempPendingTime;
    private string tempPendingSource;

    public bool SessionActive { get; private set; }
    public string ParticipantId { get; private set; }

    /// <summary>Seconds since the session started (ignores Time.timeScale).</summary>
    public float Now =>
        SessionActive
            ? Time.realtimeSinceStartup - sessionStartRealtime
            : sessionEndTime;

    private string ParticipantsPath =>
        Path.Combine(Application.persistentDataPath, ParticipantsFile);

    private string SeriesPath =>
        Path.Combine(Application.persistentDataPath, SeriesFile);

    private string QuizPath =>
        Path.Combine(Application.persistentDataPath, QuizFile);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =====================================================
    // PARTICIPANT ID + SESSION
    // =====================================================

    public string PeekNextId() => FormatId(PlayerPrefs.GetInt(CounterKey, 1));

    private static string FormatId(int n) => "P" + n.ToString("D2");

    public void BeginSession(string language)
    {
        if (SessionActive)
            EndSession("restarted");

        int n = PlayerPrefs.GetInt(CounterKey, 1);
        PlayerPrefs.SetInt(CounterKey, n + 1);
        PlayerPrefs.Save();

        fields.Clear();
        open.Clear();
        pendingLines.Clear();
        tempTotals.Clear();
        choiceAvailableAt.Clear();
        quizShownAt.Clear();
        dilemmaCounter = 0;
        tempPending = false;

        ParticipantId = FormatId(n);
        sessionStartRealtime = Time.realtimeSinceStartup;
        nextFlush = Time.realtimeSinceStartup + FlushInterval;
        nextSave = Time.realtimeSinceStartup + SaveInterval;
        SessionActive = true;

        Set("ParticipantID", ParticipantId);
        Set("NarrationLanguage", language);
        Set("SessionStartUTC", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        SaveParticipants();

        Debug.Log($"[Telemetry] Session started: {ParticipantId}. Folder: {Application.persistentDataPath}");
    }

    public void EndSession(string reason = "")
    {
        if (!SessionActive)
            return;

        if (tempPending)
            CommitTemperature();

        sessionEndTime = Time.realtimeSinceStartup - sessionStartRealtime;

        // Close every value that is still running.
        foreach (string type in new List<string>(open.Keys))
            CloseEntry(type, open[type], sessionEndTime);
        open.Clear();

        Set("SessionEndUTC", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        Set("SessionDurationSec", Num(sessionEndTime, 1));

        SessionActive = false;
        FlushLines();
        SaveParticipants();

        Debug.Log($"[Telemetry] Session ended ({reason})");
    }

    [ContextMenu("Reset participant counter to 1")]
    public void ResetCounter()
    {
        PlayerPrefs.SetInt(CounterKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[Telemetry] Participant counter reset. Next ID: P01");
    }

    [ContextMenu("Delete participants.csv and timeseries.csv")]
    public void DeleteFiles()
    {
        if (File.Exists(ParticipantsPath)) File.Delete(ParticipantsPath);
        if (File.Exists(SeriesPath)) File.Delete(SeriesPath);
        if (File.Exists(QuizPath)) File.Delete(QuizPath);
        Debug.Log("[Telemetry] Deleted telemetry files in " + Application.persistentDataPath);
    }

    // =====================================================
    // GENERIC API
    // =====================================================

    public void DeclareColumns(IEnumerable<string> columns)
    {
        foreach (string c in columns)
            if (!schema.Contains(c))
                schema.Add(c);
    }

    public void Set(string column, string value)
    {
        fields[column] = value;
    }

    private string Num(float value, int decimals = 2)
    {
        string s = value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        return decimalComma ? s.Replace('.', ',') : s;
    }

    // =====================================================
    // DILEMMAS (participants.csv)
    // =====================================================

    public void DeclareDilemmas(int count)
    {
        List<string> cols = new();

        for (int n = 1; n <= count; n++)
        {
            string p = $"Dil{n}_";
            cols.Add(p + "Name");
            cols.Add(p + "Order");
            cols.Add(p + "Shown");
            cols.Add(p + "NarrationStart");
            cols.Add(p + "NarrationEnd");
            cols.Add(p + "ChoiceAvailable");
            cols.Add(p + "TimeToChoice");
            cols.Add(p + "Choice");
            cols.Add(p + "End");
        }

        DeclareColumns(cols);
    }

    public void DilemmaShown(int number, string assetName)
    {
        if (!SessionActive) return;

        dilemmaCounter++;
        Set($"Dil{number}_Name", assetName);
        Set($"Dil{number}_Order", dilemmaCounter.ToString());
        Set($"Dil{number}_Shown", Num(Now));
    }

    public void DilemmaNarrationStart(int number)
    {
        if (SessionActive) Set($"Dil{number}_NarrationStart", Num(Now));
    }

    public void DilemmaNarrationEnd(int number)
    {
        if (SessionActive) Set($"Dil{number}_NarrationEnd", Num(Now));
    }

    public void DilemmaChoiceAvailable(int number)
    {
        if (!SessionActive) return;

        choiceAvailableAt[number] = Now;
        Set($"Dil{number}_ChoiceAvailable", Num(Now));
    }

    public void DilemmaChoice(int number, string choice)
    {
        if (!SessionActive) return;

        if (choiceAvailableAt.TryGetValue(number, out float available))
            Set($"Dil{number}_TimeToChoice", Num(Now - available));

        Set($"Dil{number}_Choice", choice);
    }

    public void DilemmaEnded(int number)
    {
        if (!SessionActive) return;

        Set($"Dil{number}_End", Num(Now));
        SaveParticipants();
    }

    // =====================================================
    // ENDING
    // =====================================================

    public void EndingReached(string endingTitle)
    {
        if (!SessionActive) return;

        Set("Ending", endingTitle);
        Set("EndingReachedSec", Num(Now));
        SaveParticipants();
    }

    // =====================================================
    // QUIZ (quiz_answers.csv)
    // =====================================================

    public void DeclareQuiz(IEnumerable<string> questionIds)
    {
        List<string> cols = new();

        foreach (string id in questionIds)
        {
            cols.Add(id + "_Answer");
            cols.Add(id + "_Time");
        }

        DeclareColumns(cols);
    }

    public void QuizQuestionShown(string questionId)
    {
        if (SessionActive) quizShownAt[questionId] = Now;
    }

    public void QuizAnswered(string questionId, string answer)
    {
        if (!SessionActive) return;

        Set(questionId + "_Answer", answer);

        if (quizShownAt.TryGetValue(questionId, out float shown))
            Set(questionId + "_Time", Num(Now - shown));

        SaveParticipants();
    }

    public void QuizPageShown()
    {
        if (!SessionActive) return;

        quizPageShownAt = Now;
        Set("Quiz_ShownAtSec", Num(Now));
    }

    /// <summary>Writes the final answer and the seconds from the page
    /// appearing until the answer was last changed.</summary>
    public void QuizAnswerFinal(string questionId, string answer, float secondsFromShown)
    {
        if (!SessionActive) return;

        Set(questionId + "_Answer", answer);
        Set(questionId + "_Time", Num(secondsFromShown));
    }

    public void QuizSubmitted()
    {
        if (!SessionActive) return;

        Set("Quiz_TotalSec", Num(Now - quizPageShownAt));
    }

    /// <summary>Writes one row per chosen option for a question.</summary>
public void QuizAnswerRows(string questionId,
                           IEnumerable<(string answer, string optionCorrect)> answers,
                           string questionCorrect, float seconds)
{
    if (!SessionActive) return;

    try
    {
        if (!File.Exists(QuizPath))
        {
            File.WriteAllText(QuizPath,
                string.Join(Sep.ToString(), QuizHeader) + Environment.NewLine,
                new UTF8Encoding(true));
        }

        StringBuilder sb = new();

        foreach (var a in answers)
        {
            sb.AppendLine(string.Join(Sep.ToString(),
                ParticipantId, questionId, a.answer,
                a.optionCorrect, questionCorrect, Num(seconds)));
        }

        File.AppendAllText(QuizPath, sb.ToString(), new UTF8Encoding(false));
    }
    catch (Exception e)
    {
        Debug.LogError("[Telemetry] Could not write quiz_answers.csv: " + e.Message);
    }
}

    // =====================================================
    // TIME SERIES (timeseries.csv)
    // =====================================================

    /// <summary>Records a new value for a type. The previous value of the
    /// same type is closed and written with its duration.</summary>
    private void LogSeries(string type, string value, float number,
                           string source, float time)
    {
        if (open.TryGetValue(type, out OpenEntry current))
        {
            if (current.value == value)
                return; // nothing changed

            CloseEntry(type, current, time);
        }

        open[type] = new OpenEntry
        {
            time = time,
            value = value,
            number = number,
            source = source ?? ""
        };
    }

    private void CloseEntry(string type, OpenEntry entry, float endTime)
    {
        float duration = Mathf.Max(0f, endTime - entry.time);

        pendingLines.Add(string.Join(Sep.ToString(),
            ParticipantId,
            Num(entry.time),
            type,
            Escape(entry.value),
            entry.source,
            Num(duration)));
    }

    public void LogMeter(string meter, string valueText)
    {
        if (!SessionActive) return;

        float.TryParse(valueText, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out float number);

        LogSeries(meter, valueText, number, "", Now);
    }

    public void LogZone(string zoneName)
    {
        if (SessionActive)
            LogSeries("Zone", zoneName, 0f, "", Now);
    }

    public void LogTemperatureStart(float value)
    {
        if (!SessionActive) return;

        Set("Temp_Start", Num(value, 1));
        LogSeries("Temperature", Num(value, 1), value, "start", Now);
    }

    /// <summary>Call on every temperature change. Rapid changes (slider
    /// drag, travel) are merged into one entry once the value settles.</summary>
    public void LogTemperature(float value, string source)
    {
        if (!SessionActive) return;

        tempPending = true;
        tempPendingValue = value;
        tempPendingTime = Now;
        tempPendingSource = source;
    }

    private void CommitTemperature()
    {
        tempPending = false;

        LogSeries("Temperature", Num(tempPendingValue, 1), tempPendingValue,
                  tempPendingSource, tempPendingTime);
    }

    /// <summary>Writes out all running values and lets them continue.
    /// Used when the app is paused, so little is lost if it gets killed.</summary>
    private void FlushOpenEntries()
    {
        float now = Now;

        foreach (string type in new List<string>(open.Keys))
        {
            OpenEntry e = open[type];
            CloseEntry(type, e, now);
            e.time = now; // the same value continues from here
        }
    }

    private void FlushLines()
    {
        if (pendingLines.Count == 0)
            return;

        try
        {
            if (!File.Exists(SeriesPath))
            {
                File.WriteAllText(SeriesPath,
                    string.Join(Sep.ToString(), SeriesHeader) + Environment.NewLine,
                    new UTF8Encoding(true)); // BOM so Excel reads æøå
            }

            File.AppendAllText(SeriesPath,
                string.Join(Environment.NewLine, pendingLines) + Environment.NewLine,
                new UTF8Encoding(false));

            pendingLines.Clear();
        }
        catch (Exception e)
        {
            // Lines stay in memory and are retried next time.
            Debug.LogError("[Telemetry] Could not write timeseries.csv: " + e.Message);
        }
    }

    // =====================================================
    // LIFECYCLE
    // =====================================================

    private void Update()
    {
        if (!SessionActive) return;

        if (tempPending && Now - tempPendingTime >= TemperatureSettleTime)
            CommitTemperature();

        if (Time.realtimeSinceStartup >= nextFlush)
        {
            nextFlush = Time.realtimeSinceStartup + FlushInterval;
            FlushLines();
        }

        if (Time.realtimeSinceStartup >= nextSave)
        {
            nextSave = Time.realtimeSinceStartup + SaveInterval;
            SaveParticipants();
        }
    }

    // iPads can kill apps without warning, so save whenever the app pauses.
    private void OnApplicationPause(bool paused)
    {
        if (!paused || !SessionActive) return;

        if (tempPending)
            CommitTemperature();

        FlushOpenEntries();
        FlushLines();
        SaveParticipants();
    }

    private void OnApplicationQuit()
    {
        if (SessionActive) EndSession("app quit");
    }

    // =====================================================
    // participants.csv
    // =====================================================


    private void SaveParticipants()
    {
        if (string.IsNullOrEmpty(ParticipantId))
            return;

        try
        {

            List<string> header = new(CoreColumns);
            List<Dictionary<string, string>> rows = LoadExisting(header);

            foreach (string col in schema)
                if (!header.Contains(col))
                    header.Add(col);

            rows.RemoveAll(r =>
                r.TryGetValue("ParticipantID", out string id) &&
                id == ParticipantId);

            rows.Add(new Dictionary<string, string>(fields));

            foreach (string key in fields.Keys)
                if (!header.Contains(key))
                    header.Add(key);

            StringBuilder sb = new();
            sb.AppendLine(string.Join(Sep, header.ConvertAll(Escape)));

            foreach (var row in rows)
            {
                List<string> cells = new();
                foreach (string col in header)
                    cells.Add(Escape(row.TryGetValue(col, out string v) ? v : ""));
                sb.AppendLine(string.Join(Sep, cells));
            }

            File.WriteAllText(ParticipantsPath, sb.ToString(), new UTF8Encoding(true));
        }
        catch (Exception e)
        {
            // On a PC this happens if the CSV is open in Excel. Close it.
            Debug.LogError("[Telemetry] Could not write participants.csv: " + e.Message);
        }
    }

    private List<Dictionary<string, string>> LoadExisting(List<string> header)
    {
        List<Dictionary<string, string>> rows = new();
        if (!File.Exists(ParticipantsPath))
            return rows;

        List<List<string>> table = ParseCsv(File.ReadAllText(ParticipantsPath));
        if (table.Count == 0)
            return rows;

        List<string> oldHeader = table[0];
        foreach (string col in oldHeader)
            if (!header.Contains(col))
                header.Add(col);

        for (int i = 1; i < table.Count; i++)
        {
            var row = new Dictionary<string, string>();
            for (int c = 0; c < oldHeader.Count && c < table[i].Count; c++)
                row[oldHeader[c]] = table[i][c];
            rows.Add(row);
        }

        return rows;
    }

    private static string Escape(string s)
    {
        if (s == null) return "";
        if (s.IndexOfAny(new[] { Sep, '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> table = new();
        List<string> row = new();
        StringBuilder cell = new();
        bool inQuotes = false;

        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text.Substring(1);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                { cell.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else cell.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == Sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n')
            {
                row.Add(cell.ToString()); cell.Clear();
                table.Add(row); row = new List<string>();
            }
            else if (c != '\r') cell.Append(c);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            table.Add(row);
        }

        return table;
    }
}