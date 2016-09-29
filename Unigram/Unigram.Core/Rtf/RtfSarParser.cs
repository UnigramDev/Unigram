/********************************************************************************
 *   This file is part of NRtfTree Library.
 *
 *   NRtfTree Library is free software; you can redistribute it and/or modify
 *   it under the terms of the GNU Lesser General Public License as published by
 *   the Free Software Foundation; either version 3 of the License, or
 *   (at your option) any later version.
 *
 *   NRtfTree Library is distributed in the hope that it will be useful,
 *   but WITHOUT ANY WARRANTY; without even the implied warranty of
 *   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *   GNU Lesser General Public License for more details.
 *
 *   You should have received a copy of the GNU Lesser General Public License
 *   along with this program. If not, see <http://www.gnu.org/licenses/>.
 ********************************************************************************/

/********************************************************************************
 * Library:		NRtfTree
 * Version:     v0.4
 * Date:		29/06/2013
 * Copyright:   2006-2013 Salvador Gomez
 * Home Page:	http://www.sgoliver.net
 * GitHub:	    https://github.com/sgolivernet/nrtftree
 * Class:		SarParser
 * Description:	Procesador abstracto utilizado por la clase RtfReader.
 * ******************************************************************************/

namespace Unigram.Core.Rtf
{
    /// <summary>
    /// Esta clase, utilizada por RtfReader, contiene todos los m�todos necesarios para tratar cada uno de 
    /// los tipos de elementos presentes en un documento RTF. Estos m�todos ser�n llamados autom�ticamente 
    /// durante el an�lisis del documento RTF realizado por la clase RtfReader.
    /// </summary>
    public abstract class RtfSarParser
    {
        /// <summary>
        /// Este m�todo se llama una s�la vez al comienzo del an�lisis del documento RTF.
        /// </summary>
        public abstract void StartRtfDocument();

        /// <summary>
        /// Este m�todo se llama una s�la vez al final del an�lisis del documento RTF.
        /// </summary>
        public abstract void EndRtfDocument();

        /// <summary>
        /// Este m�todo se llama cada vez que se lee una llave de comienzo de grupo RTF.
        /// </summary>
        public abstract void StartRtfGroup();

        /// <summary>
        /// Este m�todo se llama cada vez que se lee una llave de fin de grupo RTF.
        /// </summary>
        public abstract void EndRtfGroup();

        /// <summary>
        /// Este m�todo se llama cada vez que se lee una palabra clave RTF.
        /// </summary>
        /// <param name="key">Palabra clave leida del documento.</param>
        /// <param name="hasParameter">Indica si la palabra clave va acompa�ada de un par�metro.</param>
        /// <param name="parameter">
        /// Par�metro que acompa�a a la palabra clave. En caso de que la palabra clave no vaya acompa�ada
        /// de ning�n par�metro, es decir, que el campo hasParam sea 'false', 
        /// este campo contendr� el valor 0.
        /// </param>
        public abstract void RtfKeyword(string key, bool hasParameter, int parameter);

        /// <summary>
        /// Este m�todo se llama cada vez que se lee un s�mbolo de Control RTF.
        /// </summary>
        /// <param name="key">S�mbolo de Control leido del documento.</param>
        /// <param name="hasParameter">Indica si el s�mbolo de Control va acompa�ado de un par�metro.</param>
        /// <param name="parameter">
        /// Par�metro que acompa�a al s�mbolo de Control. En caso de que el s�mbolo de Control no vaya acompa�ado
        /// de ning�n par�metro, es decir, que el campo hasParam sea 'false', 
        /// este campo contendr� el valor 0.
        /// </param>
        public abstract void RtfControl(string key, bool hasParameter, int parameter);

        /// <summary>
        /// Este m�todo se llama cada vez que se lee un fragmento de Texto del documento RTF.
        /// </summary>
        /// <param name="text">Texto leido del documento.</param>
        public abstract void RtfText(string text);
    }
}
