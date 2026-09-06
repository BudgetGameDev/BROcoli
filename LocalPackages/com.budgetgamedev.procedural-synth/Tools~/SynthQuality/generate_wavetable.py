import math
from pathlib import Path
path=Path(__file__).resolve().parents[2]/'Runtime/Core/VoiceWavetable.cs'
header='''using Unity.Mathematics;

namespace BudgetGameDev.Synth
{
    /// <summary>Immutable 1024-point periodic tables: phase-aligned sine, saw, and
    /// hollow odd-harmonic spectrum. Seven mips retain 127/63/31/15/7/3/1 partials.
    /// Tables are generated offline as literal data, with no per-voice storage.
    /// Burst supports reading a directly accessed static readonly primitive array.
    /// Warmup must run during voice initialization, outside real-time processing.
    /// Interpolation wraps periodically; morphing and mip selection are continuous.
    /// Mip transitions use an extra octave of bandwidth headroom. PM bandwidth is
    /// a conservative estimate, not a proof of bandlimiting arbitrary modulation.</summary>
    public static class VoiceWavetable
    {
        public const int TableSize=1024;
        public const int MipCount=7;
        public const int DataBytes=(1+2*MipCount)*TableSize*sizeof(float);
        public static float Warmup() => Data[0];
        public static float Sample(float phase,float position,float bandwidthHz,float sampleRate)
        {
            phase=math.isfinite(phase)?phase-math.floor(phase):0;
            position=SynthParameters.Safe(position,0,1);
            sampleRate=SynthParameters.Safe(sampleRate,32000,384000);
            bandwidthHz=SynthParameters.Safe(bandwidthHz,.001f,10000000);
            float budget=.45f*sampleRate/bandwidthHz;
            float mip=math.clamp(1+math.log2(128/(budget+1)),0,MipCount-1);
            int low=(int)mip, high=math.min(low+1,MipCount-1);
            float t=mip-low;
            float sine=Read(0,phase);
            float saw=math.lerp(Read(1+low,phase),Read(1+high,phase),t);
            float hollow=math.lerp(Read(1+MipCount+low,phase),Read(1+MipCount+high,phase),t);
            float result=position<=.5f?math.lerp(sine,saw,position*2):math.lerp(saw,hollow,position*2-1);
            return result*math.min(1,budget);
        }
        private static float Read(int table,float phase)
        {
            float index=phase*TableSize; int i=(int)index;
            int next=(i+1)&(TableSize-1); i&=TableSize-1;
            int start=table*TableSize;
            return math.lerp(Data[start+i],Data[start+next],index-math.floor(index));
        }
        // Offline generator: sum sin(2*pi*h*phase)/h for saw; odd h only with
        // h^-0.8 for hollow. Normalize each timbre using its richest table peak;
        // use the same normalization for every mip to preserve spectral levels.
        private static readonly float[] Data=new float[]
        {
'''
n=1024
banks=[[math.sin(2*math.pi*i/n) for i in range(n)]]
for kind in ('saw','hollow'):
 tables=[]
 for maxh in (127,63,31,15,7,3,1):
  tables.append([sum(math.sin(2*math.pi*h*i/n)/(h if kind=='saw' else h**.8) for h in range(1,maxh+1) if kind=='saw' or h%2==1) for i in range(n)])
 peak=max(abs(x) for x in tables[0])
 banks.extend([[x/peak for x in table] for table in tables])
with path.open('w') as f:
 f.write(header)
 for t,table in enumerate(banks):
  f.write('            // Table '+str(t)+'\n')
  for i in range(0,n,8):f.write('            '+','.join(format(x,'.9g')+'f' if x else '0f' for x in table[i:i+8])+',\n')
 f.write('        };\n    }\n}\n')
print(path.stat().st_size)
