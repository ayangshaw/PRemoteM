﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shawn.Utils;

namespace _1RM.View.Launcher
{
    public class ServerTitleViewModel : NotifyPropertyChangedBase
    {
        public ServerTitleViewModel(string text)
        {
            var vms = text.Select(c => new CharViewModel(c)).ToList();
            _charViewModels = vms;
            Text = text;
        }

        public readonly string Text;

        private List<CharViewModel> _charViewModels;
        public List<CharViewModel> CharViewModels
        {
            get => _charViewModels;
            set => SetAndNotifyIfChanged(ref _charViewModels, value);
        }

        public void UnHighLightAll()
        {
            var vms = CharViewModels.ToArray();
            foreach (var charViewModel in vms)
            {
                charViewModel.IsHighLight = false;
            }
        }
        public void HighLight(List<bool> flags)
        {
            if (flags.Count == CharViewModels.Count)
            {
                var vms = CharViewModels.ToArray();
                for (var i = 0; i < vms.Length; i++)
                {
                    vms[i].IsHighLight = flags[i];
                }
            }
            else
            {
                UnHighLightAll();
            }
        }
    }
}
