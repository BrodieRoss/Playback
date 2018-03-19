// A simplified version of Naudios dynamic range compression algorithm, adpated to work with floats, autogain removed and removes enveloping so it is a hard knee compressor

using NAudio.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playback.DSP
{
    class Compressor
    {

        public float MakeUpGain { get; set; }
        public float Threshold { get; set; }
        public float Ratio { get; set; }

        public Compressor()
        {
             
        }


        public void compressSample(ref float sample)
        {

          double doubleSample =Convert.ToDouble( Math.Abs(sample));//returns absolute value for sample and converts to double to account for decibel conversion

           doubleSample = Decibels.LinearToDecibels(doubleSample); // convert to Decibels

            float dBOverLimit = (float)doubleSample + Threshold; //figure out how many decibels over limit the sample is
            if (dBOverLimit>0.0f)// if over the threshold then apply the compression ratio to the sample
            {
                float gainReduction = -dBOverLimit * (Ratio - 1.0f);
                gainReduction = (float)Decibels.DecibelsToLinear(gainReduction);
                sample *= gainReduction;
            }

            else // if under threshold then leave the sample be
            {
               
            }

           
        }
    }
}
