using System;
using System.Collections.Generic;
using UnityEngine;

public enum GazeAnalysisStartMode
{
    /// <summary>
    /// Addされた瞬間から正確性・安定性・注視性を評価する。
    /// </summary>
    OnAdd,

    /// <summary>
    /// 視線が初めて対象に入った瞬間から正確性・安定性・注視性を評価する。
    /// 風船割りなどでは基本こちらがおすすめ。
    /// </summary>
    OnFirstEnter
}

public enum GazeSamplingLoop
{
    Update,
    FixedUpdate
}

public enum GazeTargetShape
{
    /// <summary>
    /// UIは矩形、通常GameObjectは円形として扱う。
    /// </summary>
    Auto,

    /// <summary>
    /// 対象中心からの半径で判定する。
    /// 風船などに向いている。
    /// </summary>
    Circle,

    /// <summary>
    /// 画面上の矩形範囲で判定する。
    /// ボタンや画像などに向いている。
    /// </summary>
    Rect
}

public enum GazeTargetEndReason
{
    Removed,
    Destroyed,
    AnalyzeEnd,
    Snapshot
}

[Serializable]
public class GazeSessionResult
{
    public string sessionId;
    public string memo;

    public string startedAt;
    public string endedAt;

    public float durationSeconds;

    public int targetCount;
    public int lookedTargetCount;
    public int evaluatedTargetCount;

    public float averageFirstLookTime;
    public float averageAccuracyScore;
    public float averageStabilityScore;
    public float averageAttentionScore;
    public float averageTotalScore;

    public List<GazeTargetResult> targetResults = new List<GazeTargetResult>();

    public void RecalculateSummary()
    {
        targetCount = targetResults != null ? targetResults.Count : 0;

        if (targetCount == 0)
        {
            lookedTargetCount = 0;
            evaluatedTargetCount = 0;

            averageFirstLookTime = 0f;
            averageAccuracyScore = 0f;
            averageStabilityScore = 0f;
            averageAttentionScore = 0f;
            averageTotalScore = 0f;
            return;
        }

        int firstLookCount = 0;
        int scoreCount = 0;

        float firstLookSum = 0f;
        float accuracySum = 0f;
        float stabilitySum = 0f;
        float attentionSum = 0f;
        float totalSum = 0f;

        lookedTargetCount = 0;
        evaluatedTargetCount = 0;

        foreach (GazeTargetResult result in targetResults)
        {
            if (result.wasLooked)
            {
                lookedTargetCount++;
                firstLookCount++;
                firstLookSum += result.firstLookTime;
            }

            if (result.evaluationSampleCount > 0)
            {
                evaluatedTargetCount++;
                scoreCount++;

                accuracySum += result.accuracyScore;
                stabilitySum += result.stabilityScore;
                attentionSum += result.attentionScore;
                totalSum += result.totalScore;
            }
        }

        averageFirstLookTime = firstLookCount > 0 ? firstLookSum / firstLookCount : -1f;

        averageAccuracyScore = scoreCount > 0 ? accuracySum / scoreCount : 0f;
        averageStabilityScore = scoreCount > 0 ? stabilitySum / scoreCount : 0f;
        averageAttentionScore = scoreCount > 0 ? attentionSum / scoreCount : 0f;
        averageTotalScore = scoreCount > 0 ? totalSum / scoreCount : 0f;
    }

    public string ToJson(bool prettyPrint = true)
    {
        return JsonUtility.ToJson(this, prettyPrint);
    }
}

[Serializable]
public class GazeTargetResult
{
    public string targetId;
    public string label;
    public string group;

    public GazeTargetEndReason endReason;

    public float targetAppearTimeFromSession;
    public float lifeTime;

    /// <summary>
    /// 対象が出現してから初めて視線が対象に入るまでの時間。
    /// 見られなかった場合は -1。
    /// </summary>
    public float firstLookTime;

    public bool wasLooked;
    public bool evaluationStarted;

    /// <summary>
    /// 対象出現から評価開始までの時間。
    /// OnFirstEnterの場合、基本的にはfirstLookTimeと同じ。
    /// </summary>
    public float evaluationStartTimeFromAppear;

    /// <summary>
    /// 正確性・安定性・注視性を評価した時間。
    /// </summary>
    public float evaluationDuration;

    public int sampleCountFromAppear;
    public int insideSampleCountFromAppear;

    public float insideTimeFromAppear;
    public float insideRateFromAppear;

    public int evaluationSampleCount;
    public int evaluationInsideSampleCount;

    public float evaluationInsideTime;
    public float evaluationInsideRate;

    public float maxContinuousInsideTime;

    public Vector2 latestTargetCenterScreenPosition;
    public float latestTargetRadiusPx;
    public float averageTargetRadiusPx;

    /// <summary>
    /// 対象中心を基準にした視線ズレの平均。
    /// 動く対象でも使える「相対的な視線重心」。
    /// </summary>
    public Vector2 meanRelativeErrorPx;

    /// <summary>
    /// 各サンプル時点での、視線と対象中心の平均距離。
    /// 正確性の元データ。
    /// </summary>
    public float averageDistanceFromCenterPx;

    /// <summary>
    /// 対象中心を基準にした視線ズレの標準偏差。
    /// 安定性の元データ。
    /// </summary>
    public float relativeErrorStdDevPx;

    public float normalizedAverageDistance;

    /// <summary>
    /// 正確性：対象中心にどれだけ近いか。0〜1。
    /// </summary>
    public float accuracyScore;

    /// <summary>
    /// 安定性：対象中心を基準に視線がどれだけ散らばっていないか。0〜1。
    /// </summary>
    public float stabilityScore;

    /// <summary>
    /// 注視性：評価開始後、対象内にどれだけ視線が入っていたか。0〜1。
    /// </summary>
    public float attentionScore;

    /// <summary>
    /// 正確性・安定性・注視性の重み付き平均。0〜1。
    /// </summary>
    public float totalScore;

    /// <summary>
    /// keepRawSamples が true のときだけ保存。
    /// 通常は null。
    /// </summary>
    public List<GazeSampleRecord> rawSamples;
}

[Serializable]
public class GazeSampleRecord
{
    public float timeFromTargetAppear;
    public float timeFromEvaluationStart;

    public Vector2 gazeScreenPosition;
    public Vector2 targetCenterScreenPosition;

    public float targetRadiusPx;
    public float distanceFromCenterPx;

    public bool isInside;
    public bool isEvaluated;
}