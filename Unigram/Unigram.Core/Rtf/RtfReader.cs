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
 * Class:		RtfReader
 * Description:	Analizador secuencial de documentos RTF.
 * ******************************************************************************/

using System.IO;

namespace Unigram.Core.Rtf
{
    /// <summary>
    /// Esta clase proporciona los métodos necesarios para la carga y análisis secuencial de un documento RTF.
    /// </summary>
    public class RtfReader
    {
        #region Atributos privados

        private TextReader rtf;		//Fichero/Cadena de entrada RTF
        private RtfLex lex;		//Analizador léxico para RTF
        private RtfToken tok;		//Token actual
        private RtfSarParser reader;		//Rtf Reader

        #endregion

        #region Constructores

        /// <summary>
        /// Constructor de la clase RtfReader.
        /// </summary>
        /// <param name="reader">
        /// Objeto del tipo SARParser que contienen los métodos necesarios para el tratamiento de los
        /// distintos elementos de un documento RTF.
        /// </param>
        public RtfReader(RtfSarParser reader)
        {
            /* Inicializados por defecto */
			//lex = null;
            //tok = null;
            //rtf = null;

            this.reader = reader;
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Carga un documento RTF dada la cadena de caracteres que lo contiene.
        /// </summary>
        /// <param name="text">Cadena de caractres que contiene el documento RTF.</param>
        /// <returns>
        /// Resultado de la carga del documento. Si la carga se realiza correctamente
        /// se devuelve el valor 0.
        /// </returns>
        public int LoadRtfText(string text)
        {
            //Resultado de la carga
            int res = 0;

            //Se abre el fichero de entrada
            rtf = new StringReader(text);

            //Se crea el analizador léxico para RTF
            lex = new RtfLex(rtf);

            //Se devuelve el resultado de la carga
            return res;
        }

        /// <summary>
        /// Comienza el análisis del documento RTF y provoca la llamada a los distintos métodos 
        /// del objeto IRtfReader indicado en el constructor de la clase.
        /// </summary>
        /// <returns>
        /// Resultado del análisis del documento. Si la carga se realiza correctamente
        /// se devuelve el valor 0.
        /// </returns>
        public int Parse()
        {
            //Resultado del análisis
            int res = 0;

            //Comienza el documento
            reader.StartRtfDocument();

            //Se obtiene el primer token
            tok = lex.NextToken();

            while (tok.Type != RtfTokenType.Eof)
            {
                switch (tok.Type)
                {
                    case RtfTokenType.GroupStart:
                        reader.StartRtfGroup();
                        break;
                    case RtfTokenType.GroupEnd:
                        reader.EndRtfGroup();
                        break;
                    case RtfTokenType.Keyword:
                        reader.RtfKeyword(tok.Key, tok.HasParameter, tok.Parameter);
                        break;
                    case RtfTokenType.Control:
                        reader.RtfControl(tok.Key, tok.HasParameter, tok.Parameter);
                        break;
                    case RtfTokenType.Text:
                        reader.RtfText(tok.Key);
                        break;
                    default:
                        res = -1;
                        break;
                }

                //Se obtiene el siguiente token
                tok = lex.NextToken();
            }

            //Finaliza el documento
            reader.EndRtfDocument();

            //Se cierra el stream
            rtf.Dispose();

            return res;
        }

        #endregion
    }
}
