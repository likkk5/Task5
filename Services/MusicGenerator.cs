using NAudio.Wave;
using Melanchall.DryWetMidi.MusicTheory;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Common;

namespace MusicStoreShowcase.Services;

public class MusicGenerator
{
    private const int SampleRate = 44100;

    private readonly int[][] _progressions = new[]
    {
        new[] { 0, 5, 3, 4 },
        new[] { 0, 3, 4, 5 },
        new[] { 0, 0, 5, 5 },
        new[] { 0, 4, 5, 3 },
        new[] { 0, 5, 2, 4 },
        new[] { 0, 3, 5, 4 },
    };

    private readonly IEnumerable<Interval>[] _scaleIntervals = new[]
    {
        ScaleIntervals.Major,
        ScaleIntervals.Minor,
        ScaleIntervals.Dorian,
        ScaleIntervals.Mixolydian,
    };

    private readonly int[] _tempos = { 70, 85, 100, 115, 130, 145 };

    public byte[] GenerateWav(long seed)
    {
        var rng = new SeededRandom(seed);

        var rootNote = (NoteName)rng.Next(12);

        int scaleIndex = rng.Next(_scaleIntervals.Length);
        var scaleIntervals = _scaleIntervals[scaleIndex];
        var scale = new Scale(scaleIntervals, rootNote);

        var chordIntervals = GetChordIntervals(rng);

        var progression = _progressions[rng.Next(_progressions.Length)];
        int tempo = _tempos[rng.Next(_tempos.Length)];

        int melodyDensity = rng.Next(25, 55);
        int melodyOctave = rng.Next(5, 7);
        int chordOctave = rng.Next(3, 5);
        int noteVelocity = rng.Next(60, 100);

        var midiFile = new MidiFile();
        var trackChunk = new TrackChunk();

        int instrument = rng.Next(0, 128);
        trackChunk.Events.Add(new ProgramChangeEvent((SevenBitNumber)instrument));

        long currentTick = 0;
        long chordDuration = rng.Next(360, 600);

        for (int i = 0; i < progression.Length; i++)
        {
            int degree = progression[i] % 7;
            var chordRoot = scale.GetDegree((ScaleDegree)degree);

            int rootNumber = Melanchall.DryWetMidi.MusicTheory.Note.Get(chordRoot, chordOctave).NoteNumber;

            var chordNotes = new List<int> { rootNumber };

            foreach (int interval in chordIntervals)
            {
                if (interval == 0) continue;
                var intervalNote = (NoteName)(((int)chordRoot + interval) % 12);
                int noteNumber = Melanchall.DryWetMidi.MusicTheory.Note.Get(intervalNote, chordOctave).NoteNumber;
                chordNotes.Add(noteNumber);
            }

            long duration = chordDuration;
            if (rng.NextBool(0.3)) duration = chordDuration / 2;
            if (rng.NextBool(0.15)) duration = chordDuration * 2;

            foreach (int noteNumber in chordNotes)
            {
                AddNoteToTrack(trackChunk, (SevenBitNumber)noteNumber, currentTick, duration, noteVelocity);
            }

            currentTick += duration;
        }

        long melodyTick = 0;
        while (melodyTick < currentTick)
        {
            if (rng.NextBool(melodyDensity / 100.0))
            {
                int degree = rng.Next(0, scaleIntervals.Count());
                var scaleNote = scale.GetDegree((ScaleDegree)degree);
                int noteNumber = Melanchall.DryWetMidi.MusicTheory.Note.Get(scaleNote, melodyOctave).NoteNumber;

                long melodyDuration = rng.Next(60, 240);
                int melodyVelocity = noteVelocity + rng.Next(-20, 20);
                melodyVelocity = Math.Clamp(melodyVelocity, 40, 110);

                AddNoteToTrack(trackChunk, (SevenBitNumber)noteNumber, melodyTick, melodyDuration, melodyVelocity);
            }
            melodyTick += rng.Next(60, 180);
        }

        midiFile.Chunks.Add(trackChunk);
        return ConvertMidiToWav(midiFile, tempo);
    }

    private int[] GetChordIntervals(SeededRandom rng)
    {
        int chordType = rng.Next(0, 5);

        return chordType switch
        {
            0 => new[] { 0, 4, 7 },           
            1 => new[] { 0, 3, 7 },           
            2 => new[] { 0, 4, 7, 11 },       
            3 => new[] { 0, 3, 7, 10 },       
            _ => new[] { 0, 4, 7, 9 },        
        };
    }

    private void AddNoteToTrack(TrackChunk trackChunk, SevenBitNumber noteNumber, long startTick, long duration, int velocity = 80)
    {
        var noteOnEvent = new NoteOnEvent
        {
            NoteNumber = noteNumber,
            Velocity = (SevenBitNumber)velocity
        };
        noteOnEvent.DeltaTime = startTick;

        var noteOffEvent = new NoteOffEvent
        {
            NoteNumber = noteNumber,
            Velocity = (SevenBitNumber)0
        };
        noteOffEvent.DeltaTime = duration;

        trackChunk.Events.Add(noteOnEvent);
        trackChunk.Events.Add(noteOffEvent);
    }

    private byte[] ConvertMidiToWav(MidiFile midiFile, int tempo)
    {
        var samples = SampleRate * 10;
        var buffer = new float[samples];

        var tempoMap = midiFile.GetTempoMap();
        var notes = midiFile.GetNotes();

        foreach (var note in notes)
        {
            double freq = 440 * Math.Pow(2, (note.NoteNumber - 69) / 12.0);
            var startTime = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, tempoMap);
            var lengthTime = TimeConverter.ConvertTo<MetricTimeSpan>(note.Length, tempoMap);

            int startSample = (int)(startTime.TotalSeconds * SampleRate);
            int durationSamples = (int)(lengthTime.TotalSeconds * SampleRate);
            if (durationSamples <= 0) durationSamples = SampleRate / 4;

            for (int i = 0; i < durationSamples && startSample + i < samples; i++)
            {
                double t = i / (double)SampleRate;
                double envelope = t < 0.05 ? t / 0.05 : (t > lengthTime.TotalSeconds - 0.2 ? (lengthTime.TotalSeconds - t) / 0.2 : 1.0);
                double value = 0.15 * envelope * Math.Sin(2 * Math.PI * freq * t);
                buffer[startSample + i] += (float)value;
            }
        }

        NormalizeBuffer(buffer);
        return ConvertToWav(buffer);
    }

    private void NormalizeBuffer(float[] buffer)
    {
        float max = buffer.Max(Math.Abs);
        if (max > 0.95f)
        {
            float factor = 0.95f / max;
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] *= factor;
        }
    }

    private byte[] ConvertToWav(float[] buffer)
    {
        var pcm = new byte[buffer.Length * 2];
        for (int i = 0; i < buffer.Length; i++)
        {
            short sample = (short)(buffer[i] * 32767);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        using var ms = new MemoryStream();
        using (var writer = new WaveFileWriter(new IgnoreDisposeStream(ms),
               new WaveFormat(SampleRate, 16, 1)))
        {
            writer.Write(pcm, 0, pcm.Length);
        }
        return ms.ToArray();
    }

    private class IgnoreDisposeStream : Stream
    {
        private readonly Stream _inner;
        private long _position;

        public IgnoreDisposeStream(Stream inner)
        {
            _inner = inner;
            _position = inner.Position;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _position;
            set { _position = value; _inner.Position = value; }
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count)
        {
            _inner.Position = _position;
            int read = _inner.Read(buffer, offset, count);
            _position = _inner.Position;
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Position = _position;
            _inner.Write(buffer, offset, count);
            _position = _inner.Position;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPos = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _inner.Length + offset,
                _ => throw new ArgumentException("Invalid seek origin")
            };
            _position = newPos;
            _inner.Position = newPos;
            return newPos;
        }

        public override void SetLength(long value) => _inner.SetLength(value);
        protected override void Dispose(bool disposing) { }
    }
}