﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Common.Chemistry
{
    public class IsotopeAbundances : ImmutableDictionary<string, MassDistribution>
    {
// ReSharper disable InconsistentNaming
        public static readonly IsotopeAbundances Default;
// ReSharper restore InconsistentNaming

        private IsotopeAbundances(IDictionary<string, MassDistribution> dictionary)
            : base(dictionary)
        {
        }

        public IsotopeAbundances SetAbundances(IDictionary<string, MassDistribution> abundances)
        {
            var dict = new Dictionary<string, MassDistribution>(this);
            foreach (var entry in abundances)
            {
                dict[entry.Key] = entry.Value;
            }
            return new IsotopeAbundances(dict);
        }

        public IsotopeAbundances SetAbundances(string element, MassDistribution massDistribution)
        {
            return SetAbundances(new Dictionary<string, MassDistribution> {{element, massDistribution}});
        }
        
        public const double ISOTOPE_PCT_TRACE = double.Epsilon; // Magic number to indicate that isotope has effectively zero natural abundance

        static IsotopeAbundances()
        {
            // ReSharper disable NonLocalizedString
            var defaults = new Dictionary<string, double[]>
            {
                {"H",new []{1.0078246,0.999855,2.0141021,0.000145,3.0160492675,ISOTOPE_PCT_TRACE,}}, // H3 appears in trace amounts - but a zero abundance would cause it to be ignored
                {"He",new []{3.01603,0.00000138,4.0026,0.99999862,}},
                {"Li",new []{6.015121,0.075,7.016003,0.925,}},
                {"Be",new []{9.012182,1.0,}},
                {"B",new []{10.012937,0.199,11.009305,0.801,}},
                {"C",new []{12.0,0.98916,13.0033554,0.01084,}},
                {"N",new []{14.0030732,0.99633,15.0001088,0.00366,}},
                {"O",new []{15.9949141,0.997576009706,16.9991322,0.000378998479,17.9991616,0.002044991815,}},
                {"F",new []{18.9984032,1.0,}},
                {"Ne",new []{19.992435,0.9048,20.993843,0.0027,21.991383,0.0925,}},
                {"Na",new []{22.989767,1.0,}},
                {"Mg",new []{23.985042,0.7899,24.985837,0.1,25.982593,0.1101,}},
                {"Al",new []{26.981539,1.0,}},
                {"Si",new []{27.976927,0.9223,28.976495,0.0467,29.97377,0.031,}},
                {"P",new []{30.973762,1.0, 31.973907274, ISOTOPE_PCT_TRACE,}}, // P32 appears in trace amounts - but a zero abundance would cause it to be ignored
                {"S",new []{31.97207,0.95021,32.971456,0.00745,33.967866,0.04221,35.96708,0.00013,}},
                {"Cl",new []{34.9688531,0.75529,36.9659034,0.24471,}},
                {"Ar",new []{35.967545,0.00337,37.962732,0.00063,39.962384,0.996,}},
                {"K",new []{38.963707,0.932581,39.963999,0.000117,40.961825,0.067302,}},
                {"Ca",new []{39.962591,0.96941,41.958618,0.00647,42.958766,0.00135,43.95548,0.02086,45.953689,0.00004,47.952533,0.00187,}},
                {"Sc",new []{44.95591,1.0,}},
                {"Ti",new []{45.952629,0.08,46.951764,0.073,47.947947,0.738,48.947871,0.055,49.944792,0.054,}},
                {"V",new []{49.947161,0.0025,50.943962,0.9975,}},
                {"Cr",new []{49.946046,0.04345,51.940509,0.8379,52.940651,0.095,53.938882,0.02365,}},
                {"Mn",new []{54.938047,1.0,}},
                {"Fe",new []{53.939612,0.059,55.934939,0.9172,56.935396,0.021,57.933277,0.0028,}},
                {"Co",new []{58.933198,1.0,}},
                {"Ni",new []{57.935346,0.6827,59.930788,0.261,60.931058,0.0113,61.928346,0.0359,63.927968,0.0091,}},
                {"Cu",new []{62.939598,0.6917,64.927793,0.3083,}},
                {"Zn",new []{63.929145,0.486,65.926034,0.279,66.927129,0.041,67.924846,0.188,69.925325,0.006,}},
                {"Ga",new []{68.92558,0.60108,70.9247,0.39892,}},
                {"Ge",new []{69.92425,0.205,71.922079,0.274,72.923463,0.078,73.921177,0.365,75.921401,0.078,}},
                {"As",new []{74.921594,1.0,}},
                {"Se",new []{73.922475,0.009,75.919212,0.091,76.919912,0.076,77.919,0.236,79.91652,0.499,81.916698,0.089,}},
                {"Br",new []{78.918336,0.5069,80.916289,0.4931,}},
                {"Kr",new []{77.914,0.0035,79.91638,0.0225,81.913482,0.116,82.914135,0.115,83.911507,0.57,85.910616,0.173,}},
                {"Rb",new []{84.911794,0.7217,86.909187,0.2783,}},
                {"Sr",new []{83.91343,0.0056,85.909267,0.0986,86.908884,0.07,87.905619,0.8258,}},
                {"Y",new []{88.905849,1.0,}},
                {"Zr",new []{89.904703,0.5145,90.905644,0.1122,91.905039,0.1715,93.906314,0.1738,95.908275,0.028,}},
                {"Nb",new []{92.906377,1.0,}},
                {"Mo",new []{91.906808,0.1484,93.905085,0.0925,94.90584,0.1592,95.904678,0.1668,96.90602,0.0955,97.905406,0.2413,99.907477,0.0963,}},
                {"Tc",new []{98.0,1.0,}},
                {"Ru",new []{95.907599,0.0554,97.905287,0.0186,98.905939,0.127,99.904219,0.126,100.905582,0.171,101.904348,0.316,103.905424,0.186,}},
                {"Rh",new []{102.9055,1.0,}},
                {"Pd",new []{101.905634,0.0102,103.904029,0.1114,104.905079,0.2233,105.903478,0.2733,107.903895,0.2646,109.905167,0.1172,}},
                {"Ag",new []{106.905092,0.51839,108.904757,0.48161,}},
                {"Cd",new []{105.906461,0.0125,107.904176,0.0089,109.903005,0.1249,110.904182,0.128,111.902758,0.2413,112.9044,0.1222,113.903357,0.2873,115.904754,0.0749,}},
                {"In",new []{112.904061,0.043,114.90388,0.957,}},
                {"Sn",new []{111.904826,0.0097,113.902784,0.0065,114.903348,0.0036,115.901747,0.1453,116.902956,0.0768,117.901609,0.2422,118.90331,0.0858,119.9022,0.3259,121.90344,0.0463,123.905274,0.0579,}},
                {"Sb",new []{120.903821,0.574,122.904216,0.426,}},
                {"Te",new []{119.904048,0.00095,121.903054,0.0259,122.904271,0.00905,123.902823,0.0479,124.904433,0.0712,125.903314,0.1893,127.904463,0.317,129.906229,0.3387,}},
                {"I",new []{126.904473,1.0,}},
                {"Xe",new []{123.905894,0.001,125.904281,0.0009,127.903531,0.0191,128.90478,0.264,129.903509,0.041,130.905072,0.212,131.904144,0.269,133.905395,0.104,135.907214,0.089,}},
                {"Cs",new []{132.905429,1.0,}},
                {"Ba",new []{129.906282,0.00106,131.905042,0.00101,133.904486,0.0242,134.905665,0.06593,135.904553,0.0785,136.905812,0.1123,137.905232,0.717,}},
                {"La",new []{137.90711,0.0009,138.906347,0.9991,}},
                {"Ce",new []{135.90714,0.0019,137.905985,0.0025,139.905433,0.8843,141.909241,0.1113,}},
                {"Pr",new []{140.907647,1.0,}},
                {"Nd",new []{141.907719,0.2713,142.90981,0.1218,143.910083,0.238,144.91257,0.083,145.913113,0.1719,147.916889,0.0576,149.920887,0.0564,}},
                {"Pm",new []{145.0,1.0,}},
                {"Sm",new []{143.911998,0.031,146.914895,0.15,147.91482,0.113,148.917181,0.138,149.917273,0.074,151.919729,0.267,153.922206,0.227,}},
                {"Eu",new []{150.919847,0.478,152.921225,0.522,}},
                {"Gd",new []{151.919786,0.002,153.920861,0.0218,154.922618,0.148,155.922118,0.2047,156.923956,0.1565,157.924099,0.2484,159.927049,0.2186,}},
                {"Tb",new []{158.925342,1.0,}},
                {"Dy",new []{155.925277,0.0006,157.924403,0.001,159.925193,0.0234,160.92693,0.189,161.926795,0.255,162.928728,0.249,163.929171,0.282,}},
                {"Ho",new []{164.930319,1.0,}},
                {"Er",new []{161.928775,0.0014,163.929198,0.0161,165.93029,0.336,166.932046,0.2295,167.932368,0.268,169.935461,0.149,}},
                {"Tm",new []{168.934212,1.0,}},
                {"Yb",new []{167.933894,0.0013,169.934759,0.0305,170.936323,0.143,171.936378,0.219,172.938208,0.1612,173.938859,0.318,175.942564,0.127,}},
                {"Lu",new []{174.94077,0.9741,175.942679,0.0259,}},
                {"Hf",new []{173.940044,0.00162,175.941406,0.05206,176.943217,0.18606,177.943696,0.27297,178.945812,0.13629,179.946545,0.351,}},
                {"Ta",new []{179.947462,0.00012,180.947992,0.99988,}},
                {"W",new []{179.946701,0.0012,181.948202,0.263,182.95022,0.1428,183.950928,0.307,185.954357,0.286,}},
                {"Re",new []{184.952951,0.374,186.955744,0.626,}},
                {"Os",new []{183.952488,0.0002,185.95383,0.0158,186.955741,0.016,187.95586,0.133,188.958137,0.161,189.958436,0.264,191.961467,0.41,}},
                {"Ir",new []{190.960584,0.373,192.962917,0.627,}},
                {"Pt",new []{189.959917,0.0001,191.961019,0.0079,193.962655,0.329,194.964766,0.338,195.964926,0.253,197.967869,0.072,}},
                {"Au",new []{196.966543,1.0,}},
                {"Hg",new []{195.965807,0.0015,197.966743,0.1,198.968254,0.169,199.9683,0.231,200.970277,0.132,201.970617,0.298,203.973467,0.0685,}},
                {"Tl",new []{202.97232,0.29524,204.974401,0.70476,}},
                {"Pb",new []{203.97302,0.014,205.97444,0.241,206.975872,0.221,207.976627,0.524,}},
                {"Bi",new []{208.980374,1.0,}},
                {"Po",new []{209.0,1.0,}},
                {"At",new []{210.0,1.0,}},
                {"Rn",new []{222.0,1.0,}},
                {"Fr",new []{223.0,1.0,}},
                {"Ra",new []{226.025,1.0,}},
                {"Ac",new []{227.028,1.0,}},
                {"Th",new []{232.038054,1.0,}},
                {"Pa",new []{231.0359,1.0,}},
                {"U",new []{234.040946,0.000055,235.043924,0.0072,238.050784,0.992745,}},
                {"Np",new []{237.048,1.0,}},
                {"Pu",new []{244.0,1.0,}},
                {"Am",new []{243.0,1.0,}},
                {"Cm",new []{247.0,1.0,}},
                {"Bk",new []{247.0,1.0,}},
                {"Cf",new []{251.0,1.0,}},
                {"Es",new []{252.0,1.0,}},
                {"Fm",new []{257.0,1.0,}},
                {"Md",new []{258.0,1.0,}},
                {"No",new []{259.0,1.0,}},
                {"Lr",new []{260.0,1.0,}},
            };
            // ReSharper restore NonLocalizedString

            var dict = new Dictionary<string, MassDistribution>();
            foreach (var entry in defaults)
            {
                var isotopes = new Dictionary<double, double>();
                for (int i = 0; i < entry.Value.Length; i += 2)
                {
                    isotopes.Add(entry.Value[i], entry.Value[i + 1]);
                }
                dict.Add(entry.Key, MassDistribution.NewInstance(isotopes, 0, 0));
            }
            Default = new IsotopeAbundances(dict);
        }
    }
}
