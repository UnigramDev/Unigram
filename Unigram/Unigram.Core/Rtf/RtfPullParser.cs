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
 * Class:		RtfPullParser
 * Description:	Pull parser para documentos RTF.
 * ******************************************************************************/

using System.IO;

namespace Unigram.Core.Rtf
{
    /// <summary>
    /// Pull parser para documentos RTF.
    /// </summary>
    public class RtfPullParser
    {
        #region Constantes

        public const int START_DOCUMENT = 0;
        public const int END_DOCUMENT = 1;
        public const int KEYWORD = 2;
        public const int CONTROL = 3;
        public const int START_GROUP = 4;
        public const int END_GROUP = 5;
        public const int TEXT = 6;

        #endregion

        #region Atributos

        private TextReader rtf;		//Fichero/Cadena de entrada RTF
        private RtfLex lex;		    //Analizador léxico para RTF
        private RtfToken tok;		//Token actual
        private int currentEvent;   //Evento actual

        #endregion

        #region Construtores

        /// <summary>
        /// Constructor de la clase.
        /// </summary>
        public RtfPullParser()
        {
            currentEvent = START_DOCUMENT;
        }

        /// <summary>
        /// Carga una cadena de Texto con formato RTF.
        /// </summary>
        /// <param name="text">Cadena de Texto que contiene el documento.</param>
        public int LoadRtfText(string text)
        {
            int res = 0;

            //Se abre el fichero de entrada
            rtf = new StringReader(text);

            //Se crea el analizador léxico para RTF
            lex = new RtfLex(rtf);

            return res;
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Obtiene el tipo de evento actual.
        /// </summary>
        /// <returns>Tipo de evento actual.</returns>
        public int GetEventType()
        {
            return currentEvent;
        }

        /// <summary>
        /// Obtiene el siguiente elemento del documento.
        /// </summary>
        /// <returns>Siguiente elemento del documento.</returns>
        public int Next()
        {
            tok = lex.NextToken();

            switch (tok.Type)
            {
                case RtfTokenType.GroupStart:
                    currentEvent = START_GROUP;
                    break;
                case RtfTokenType.GroupEnd:
                    currentEvent = END_GROUP;
                    break;
                case RtfTokenType.Keyword:
                    currentEvent = KEYWORD;
                    break;
                case RtfTokenType.Control:
                    currentEvent = CONTROL;
                    break;
                case RtfTokenType.Text:
                    currentEvent = TEXT;
                    break;
                case RtfTokenType.Eof:
                    currentEvent = END_DOCUMENT;
                    break;
            }

            return currentEvent;
        }

        /// <summary>
        /// Obtiene la palabra clave / símbolo control del elemento actual.
        /// </summary>
        /// <returns>Palabra clave / símbolo control del elemento actual.</returns>
        public string GetName()
        {
            return tok.Key;
        }

        /// <summary>
        /// Obtiene el parámetro del elemento actual.
        /// </summary>
        /// <returns>Parámetro del elemento actual.</returns>
        public int GetParam()
        {
            return tok.Parameter;
        }

        /// <summary>
        /// Consulta si el elemento actual tiene parámetro.
        /// </summary>
        /// <returns>Devuelve TRUE si el elemento actual tiene parámetro.</returns>
        public bool HasParam()
        {
            return tok.HasParameter;
        }

        /// <summary>
        /// Obtiene el texto del elemento actual.
        /// </summary>
        /// <returns>Texto del elemento actual.</returns>
        public string GetText()
        {
            return tok.Key;
        }

        #endregion
    }
}
