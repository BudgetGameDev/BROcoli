from pathlib import Path
import numpy as np
from scipy.signal import resample_poly, firwin
from scipy.io.wavfile import write
import json, sys
root=Path(sys.argv[1] if len(sys.argv)>1 else "Artifacts/Synth/Quality")
results=[]
for name in ('saw_clean','sine_driven','sine_pm','wavetable_pm'):
 for rate,quality in ((48000,1),(48000,4),(96000,4)):
  x=np.fromfile(root/f'{name}_{rate}_{quality}x.f32',dtype='<f4').astype(float)
  if rate==96000:x=resample_poly(x,1,2,window=firwin(1025,.475,window=('kaiser',12.0)))
  x=x[12000:60000] # coherent 1 second steady-state excerpt, no resampler ends
  rms=np.sqrt(np.mean(x*x))
  y=x*np.hanning(len(x));power=abs(np.fft.rfft(y))**2
  fundamental=440 if name.endswith('pm') else 880
  mask=np.zeros(len(power),bool)
  for h in range(0,24000//fundamental+1):
   k=h*fundamental;mask[max(0,k-5):min(len(mask),k+6)]=True
  out=10*np.log10(power[~mask].sum()/power.sum())
  band=np.arange(len(power))<20000
  audible=10*np.log10(power[(~mask)&band].sum()/power[band].sum())
  row=dict(case=name,output_rate=rate,oversampling=quality,off_harmonic_energy_dbc=round(float(out),3),off_harmonic_below20k_dbc=round(float(audible),3),rms=round(float(rms),6),peak=round(float(abs(x).max()),6))
  results.append(row);print(row)
  # Each comparison normalized to -18dBFS RMS for human review. No listening claim.
  matched=x*(10**(-18/20)/max(rms,1e-9));write(root/f'{name}_{rate}_{quality}x_matched.wav',48000,np.int16(np.clip(matched,-1,1)*32767))
(root/'quality-results.json').write_text(json.dumps(results,indent=2))
