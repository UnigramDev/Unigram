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
 * Class:		RtfLex
 * Description:	Analizador léxico de documentos RTF.
 * ******************************************************************************/

using System;
using System.IO;
using System.Text;

namespace Unigram.Core.Rtf
{
    /// <summary>
    /// Analizador léxico (tokenizador) para documentos en formato RTF. Analiza el documento y devuelve de 
    /// forma secuencial todos los elementos RTF leidos (tokens).
    /// </summary>
    public class RtfLex
    {
        #region Atributos privados

        /// <summary>
        /// Fichero abierto.
        /// </summary>
        private TextReader rtf;

        /// <summary>
        /// StringBuilder para construir la palabra clave / fragmento de texto
        /// </summary>
        private StringBuilder keysb;

        /// <summary>
        /// StringBuilder para construir el parámetro de la palabra clave
        /// </summary>
        private StringBuilder parsb;

        /// <summary>
        /// Caracter leido del documento
        /// </summary>
        private int c;

        #endregion

        #region Constantes

        /// <summary>
        /// Marca de fin de fichero.
        /// </summary>
        private const int Eof = -1;

        #endregion

        #region Constructores

        /// <summary>
        /// Constructor de la clase RtfLex
        /// </summary>
        /// <param name="rtfReader">Stream del fichero a analizar.</param>
        public RtfLex(TextReader rtfReader)
        {
            rtf = rtfReader;

            keysb = new StringBuilder();
            parsb = new StringBuilder();

            //Se lee el primer caracter del documento
            c = rtf.Read();
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Lee un nuevo token del documento RTF.
        /// </summary>
        /// <returns>Siguiente token leido del documento.</returns>
        public RtfToken NextToken()
        {
            //Se crea el nuevo token a devolver
            RtfToken token = new RtfToken();

            //Se ignoran los retornos de carro, tabuladores y caracteres nulos
            while (c == '\r' || c == '\n' || c == '\t' || c == '\0')
                c = rtf.Read();

            //Se trata el caracter leido
            if (c != Eof)
            {
                switch (c)
                {
                    case '{':
                        token.Type = RtfTokenType.GroupStart;
                        c = rtf.Read();
                        break;
                    case '}':
                        token.Type = RtfTokenType.GroupEnd;
                        c = rtf.Read();
                        break;
                    case '\\':
                        parseKeyword(token);
                        break;
                    default:
                        token.Type = RtfTokenType.Text;
                        parseText(token);
                        break;
                }
            }
            else
            {
                //Fin de fichero
                token.Type = RtfTokenType.Eof;
            }

            return token;
        }


        #endregion

        #region Métodos Privados

        /// <summary>
        /// Lee una palabra clave del documento RTF.
        /// </summary>
        /// <param name="token">Token RTF al que se asignará la palabra clave.</param>
        private void parseKeyword(RtfToken token)
        {
            //Se limpian los StringBuilders
            keysb.Length = 0;
            parsb.Length = 0;

            int parametroInt = 0;
            bool negativo = false;

            c = rtf.Read();

            //Si el caracter leido no es una letra --> Se trata de un símbolo de Control o un caracter especial: '\\', '\{' o '\}'
            if (!Char.IsLetter((char)c))
            {
                if (c == '\\' || c == '{' || c == '}')  //Caracter especial
                {
                    token.Type = RtfTokenType.Text;
                    token.Key = ((char)c).ToString();
                }
                else   //Simbolo de control
                {
                    token.Type = RtfTokenType.Control;
                    token.Key = ((char)c).ToString();

                    //Si se trata de un caracter especial (codigo de 8 bits) se lee el parámetro hexadecimal
                    if (token.Key == "\'")
                    {
                        string cod = "";

                        cod += (char)rtf.Read();
                        cod += (char)rtf.Read();

                        token.HasParameter = true;

                        token.Parameter = Convert.ToInt32(cod, 16);
                    }

                    //TODO: ¿Hay más símbolos de Control con parámetros?
                }

                c = rtf.Read();
            }
            else  //El caracter leido es una letra
            {
                //Se lee la palabra clave completa (hasta encontrar un caracter no alfanumérico, por ejemplo '\' ó ' '
                while (Char.IsLetter((char)c))
                {
                    keysb.Append((char)c);

                    c = rtf.Read();
                }

                //Se asigna la palabra clave leida
                token.Type = RtfTokenType.Keyword;
                token.Key = keysb.ToString();

                //Se comprueba si la palabra clave tiene parámetro
                if (Char.IsDigit((char)c) || c == '-')
                {
                    token.HasParameter = true;

                    //Se comprubea si el parámetro es negativo
                    if (c == '-')
                    {
                        negativo = true;

                        c = rtf.Read();
                    }

                    //Se lee el parámetro completo
                    while (Char.IsDigit((char)c))
                    {
                        parsb.Append((char)c);

                        c = rtf.Read();
                    }

                    parametroInt = Convert.ToInt32(parsb.ToString());

                    if (negativo)
                        parametroInt = -parametroInt;

                    //Se asigna el parámetro de la palabra clave
                    token.Parameter = parametroInt;
                }

                if (c == ' ')
                {
                    c = rtf.Read();
                }
            }
        }

        /// <summary>
        /// Lee una cadena de Texto del documento RTF.
        /// </summary>
        /// <param name="token">Token RTF al que se asignará la palabra clave.</param>
        private void parseText(RtfToken token)
        {
            //Se limpia el StringBuilder
            keysb.Length = 0;

            while (c != '\\' && c != '}' && c != '{' && c != Eof)
            {
                keysb.Append((char)c);

                c = rtf.Read();

                //Se ignoran los retornos de carro, tabuladores y caracteres nulos
                while (c == '\r' || c == '\n' || c == '\t' || c == '\0')
                    c = rtf.Read();
            }

            token.Key = keysb.ToString();
        }

        #endregion
    }
}
