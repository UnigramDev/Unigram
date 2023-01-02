//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;

namespace Unigram.Logs
{
    public interface ILogListener
    {
        void Log(LogLevel level, Type type, string message, string member, string filePath, int line);
        void Log(LogLevel level, string message, string member, string filePath, int line);
    }
}
