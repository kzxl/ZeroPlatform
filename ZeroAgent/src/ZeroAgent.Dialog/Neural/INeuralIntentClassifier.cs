using System;
using System.Collections.Generic;
using ZeroAgent.Dialog.DST;

namespace ZeroAgent.Dialog.Neural
{
    /// <summary>
    /// Contract for deep neural intent classifiers operating on lexical/semantic embeddings.
    /// </summary>
    public interface INeuralIntentClassifier
    {
        /// <summary>
        /// Gets whether the neural network has been trained and initialized.
        /// </summary>
        bool IsTrained { get; }

        /// <summary>
        /// Number of distinct intent classes the network was trained on.
        /// </summary>
        int ClassCount { get; }

        /// <summary>
        /// Trains the deep neural network on the provided intents and their sample utterances.
        /// </summary>
        /// <param name="intents">List of intents with sample utterances.</param>
        /// <param name="epochs">Number of training epochs (default: 80).</param>
        /// <param name="learningRate">Learning rate for AdamW optimizer (default: 0.05f).</param>
        void Train(IReadOnlyList<DialogueIntent> intents, int epochs = 80, float learningRate = 0.05f);

        /// <summary>
        /// Predicts the target intent given a 128-dimensional embedding vector.
        /// </summary>
        /// <param name="embedding">128-d input embedding vector.</param>
        /// <returns>Best matching intent, confidence score [0..1], and all class probabilities.</returns>
        (DialogueIntent? Intent, float Confidence, float[] Probabilities) Predict(ReadOnlySpan<float> embedding);

        /// <summary>
        /// Predicts the target intent given raw text utterance.
        /// </summary>
        /// <param name="utterance">Input user utterance.</param>
        /// <returns>Best matching intent, confidence score [0..1], and all class probabilities.</returns>
        (DialogueIntent? Intent, float Confidence, float[] Probabilities) Predict(string utterance);
    }
}
