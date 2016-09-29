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
    /// Esta clase, utilizada por RtfReader, contiene todos los métodos necesarios para tratar cada uno de 
    /// los tipos de elementos presentes en un documento RTF. Estos métodos serán llamados automáticamente 
    /// durante el análisis del documento RTF realizado por la clase RtfReader.
    /// </summary>
    public abstract class RtfSarParser
    {
        /// <summary>
        /// Este método se llama una sóla vez al comienzo del análisis del documento RTF.
        /// </summary>
        public abstract void StartRtfDocument();

        /// <summary>
        /// Este método se llama una sóla vez al final del análisis del documento RTF.
        /// </summary>
        public abstract void EndRtfDocument();

        /// <summary>
        /// Este método se llama cada vez que se lee una llave de comienzo de grupo RTF.
        /// </summary>
        public abstract void StartRtfGroup();

        /// <summary>
        /// Este método se llama cada vez que se lee una llave de fin de grupo RTF.
        /// </summary>
        public abstract void EndRtfGroup();

        /// <summary>
        /// Este método se llama cada vez que se lee una palabra clave RTF.
        /// </summary>
        /// <param name="key">Palabra clave leida del documento.</param>
        /// <param name="hasParameter">Indica si la palabra clave va acompañada de un parámetro.</param>
        /// <param name="parameter">
        /// Parámetro que acompaña a la palabra clave. En caso de que la palabra clave no vaya acompañada
        /// de ningún parámetro, es decir, que el campo hasParam sea 'false', 
        /// este campo contendrá el valor 0.
        /// </param>
        public abstract void RtfKeyword(string key, bool hasParameter, int parameter);

        /// <summary>
        /// Este método se llama cada vez que se lee un símbolo de Control RTF.
        /// </summary>
        /// <param name="key">Símbolo de Control leido del documento.</param>
        /// <param name="hasParameter">Indica si el símbolo de Control va acompañado de un parámetro.</param>
        /// <param name="parameter">
        /// Parámetro que acompaña al símbolo de Control. En caso de que el símbolo de Control no vaya acompañado
        /// de ningún parámetro, es decir, que el campo hasParam sea 'false', 
        /// este campo contendrá el valor 0.
        /// </param>
        public abstract void RtfControl(string key, bool hasParameter, int parameter);

        /// <summary>
        /// Este método se llama cada vez que se lee un fragmento de Texto del documento RTF.
        /// </summary>
        /// <param name="text">Texto leido del documento.</param>
        public abstract void RtfText(string text);
    }
}
