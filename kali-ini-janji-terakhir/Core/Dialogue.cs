using System;
using System.Collections.Generic;
using kali_ini_janji_terakhir.Farm;

namespace kali_ini_janji_terakhir.Core
{
    /// <summary>Satu percakapan: beberapa halaman teks, lalu (opsional) pilihan jawaban.</summary>
    public sealed class Dialogue
    {
        private readonly List<string> pages = new();
        private readonly List<(string Label, Action<FarmWorld> Effect)> choices = new();

        public Dialogue(string speaker, params string[] text)
        {
            Speaker = speaker;
            pages.AddRange(text);
        }

        public string Speaker { get; }
        public IReadOnlyList<string> Pages => pages;
        public IReadOnlyList<(string Label, Action<FarmWorld> Effect)> Choices => choices;

        public Dialogue Then(string text)
        {
            pages.Add(text);
            return this;
        }

        public Dialogue Choice(string label, Action<FarmWorld> effect)
        {
            choices.Add((label, effect));
            return this;
        }
    }
}
