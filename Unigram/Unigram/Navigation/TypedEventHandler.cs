//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.ComponentModel;

namespace Unigram.Navigation
{
    public class CancelEventArgs<T> : CancelEventArgs
    {
        public CancelEventArgs(T value)
        {
            Value = value;
        }

        public T Value { get; private set; }
    }
}
