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
 * Class:		RtfTree
 * Description:	Representa un documento RTF en forma de árbol.
 * ******************************************************************************/

using System;
using System.IO;
using System.Text;
using Windows.UI;

namespace Unigram.Core.Rtf
{
    /// <summary>
    /// Reresenta la estructura en forma de árbol de un documento RTF.
    /// </summary>
    public class RtfTree
    {
		#region Atributos privados

        /// <summary>
        /// Nodo raíz del documento RTF.
        /// </summary>
        private RtfTreeNode rootNode;
        /// <summary>
        /// Fichero/Cadena de entrada RTF
        /// </summary>
        private TextReader rtf;
        /// <summary>
        /// Analizador léxico para RTF
        /// </summary>
        private RtfLex lex;
        /// <summary>
        /// Token actual
        /// </summary>
        private RtfToken tok;
        /// <summary>
        /// Profundidad del nodo actual
        /// </summary>
        private int level;
        /// <summary>
        /// Indica si se decodifican los caracteres especiales (\') uniéndolos a nodos de texto contiguos.
        /// </summary>
        private bool mergeSpecialCharacters;

        #endregion

        #region Contructores

        /// <summary>
        /// Constructor de la clase RtfTree.
        /// </summary>
        public RtfTree()
        {
            //Se crea el nodo raíz del documento
            rootNode = new RtfTreeNode(RtfNodeType.Root,"ROOT",false,0);

            rootNode.Tree = this;

			/* Inicializados por defecto */

            //Se inicializa la propiedad mergeSpecialCharacters
            mergeSpecialCharacters = false;

            //Se inicializa la profundidad actual
            //level = 0;
        }

        #endregion

        #region Métodos Públicos

        /// <summary>
        /// Realiza una copia exacta del árbol RTF.
        /// </summary>
        /// <returns>Devuelve una copia exacta del árbol RTF.</returns>
        public RtfTree CloneTree()
        {
            RtfTree clon = new RtfTree();

            clon.rootNode = rootNode.CloneNode();

            return clon;
        }

        /// <summary>
        /// Carga una cadena de Texto con formato RTF.
        /// </summary>
        /// <param name="text">Cadena de Texto que contiene el documento.</param>
        /// <returns>Se devuelve el valor 0 en caso de no producirse ningún error en la carga del documento.
        /// En caso contrario se devuelve el valor -1.</returns>
        public int LoadRtfText(string text)
        {
            //Resultado de la carga
            int res = 0;

            //Se abre el fichero de entrada
            rtf = new StringReader(text);

            //Se crea el analizador léxico para RTF
            lex = new RtfLex(rtf);

            //Se carga el árbol con el contenido del documento RTF
            res = parseRtfTree();

            //Se cierra el stream
            rtf.Dispose();

            //Se devuelve el resultado de la carga
            return res;
        }

        /// <summary>
        /// Devuelve una representación Textual del documento cargado.
        /// </summary>
        /// <returns>Cadena de caracteres con la representación del documento.</returns>
        public override string ToString()
        {
            string res = "";

            res = toStringInm(rootNode, 0, false);

            return res;
        }

        /// <summary>
        /// Devuelve una representación Textual del documento cargado. Añade el tipo de nodo a la izquierda del contenido del nodo.
        /// </summary>
        /// <returns>Cadena de caracteres con la representación del documento.</returns>
        public string ToStringEx()
        {
            string res = "";

            res = toStringInm(rootNode, 0, true);

            return res;
        }

        /// <summary>
        /// Devuelve la tabla de fuentes del documento RTF.
        /// </summary>
        /// <returns>Tabla de fuentes del documento RTF</returns>
        public RtfFontTable GetFontTable()
        {
            RtfFontTable tablaFuentes = new RtfFontTable();

			//Nodo raiz del documento
			RtfTreeNode root = rootNode;

			//Grupo principal del documento
			RtfTreeNode nprin = root.FirstChild;

            //Buscamos la tabla de fuentes en el árbol
            bool enc = false;
            int i = 0;
            RtfTreeNode ntf = new RtfTreeNode();  //Nodo con la tabla de fuentes

            while (!enc && i < nprin.ChildNodes.Count)
            {
                if (nprin.ChildNodes[i].NodeType == RtfNodeType.Group &&
                    nprin.ChildNodes[i].FirstChild.NodeKey == "fonttbl")
                {
                    enc = true;
                    ntf = nprin.ChildNodes[i];
                }

                i++;
            }

            //Rellenamos el array de fuentes
            for (int j = 1; j < ntf.ChildNodes.Count; j++)
            {
                RtfTreeNode fuente = ntf.ChildNodes[j];

                int indiceFuente = -1;
                string nombreFuente = null;

                foreach (RtfTreeNode nodo in fuente.ChildNodes)
                {
                    if (nodo.NodeKey == "f")
                        indiceFuente = nodo.Parameter;

                    if (nodo.NodeType == RtfNodeType.Text)
                        nombreFuente = nodo.NodeKey.Substring(0, nodo.NodeKey.Length - 1);
                }

                tablaFuentes.AddFont(indiceFuente, nombreFuente);
            }

            return tablaFuentes;
        }

        /// <summary>
        /// Devuelve la tabla de colores del documento RTF.
        /// </summary>
        /// <returns>Tabla de colores del documento RTF</returns>
        public RtfColorTable GetColorTable()
        {
            RtfColorTable tablaColores = new RtfColorTable();

            //Nodo raiz del documento
            RtfTreeNode root = rootNode;

            //Grupo principal del documento
            RtfTreeNode nprin = root.FirstChild;

            //Buscamos la tabla de colores en el árbol
            bool enc = false;
            int i = 0;
            RtfTreeNode ntc = new RtfTreeNode();  //Nodo con la tabla de fuentes

            while (!enc && i < nprin.ChildNodes.Count)
            {
                if (nprin.ChildNodes[i].NodeType == RtfNodeType.Group &&
                    nprin.ChildNodes[i].FirstChild.NodeKey == "colortbl")
                {
                    enc = true;
                    ntc = nprin.ChildNodes[i];
                }

                i++;
            }

            //Rellenamos el array de colores
            byte rojo = 0;
            byte verde = 0;
            byte azul = 0;

            //Añadimos el color por defecto, en este caso el negro.
            //tabla.Add(Color.FromArgb(rojo,verde,azul));

            for (int j = 1; j < ntc.ChildNodes.Count; j++)
            {
                RtfTreeNode nodo = ntc.ChildNodes[j];

                if (nodo.NodeType == RtfNodeType.Text && nodo.NodeKey.Trim() == ";")
                {
                    tablaColores.AddColor(Color.FromArgb(0xFF, rojo, verde, azul));

                    rojo = 0;
                    verde = 0;
                    azul = 0;
                }
                else if (nodo.NodeType == RtfNodeType.Keyword)
                {
                    switch (nodo.NodeKey)
                    {
                        case "red":
                            rojo = (byte)nodo.Parameter;
                            break;
                        case "green":
                            verde = (byte)nodo.Parameter;
                            break;
                        case "blue":
                            azul = (byte)nodo.Parameter;
                            break;
                    }
                }
            }

            return tablaColores;
        }

        /// <summary>
        /// Devuelve la tabla de códigos con la que está codificado el documento RTF.
        /// </summary>
        /// <returns>Tabla de códigos del documento RTF. Si no está especificada en el documento se devuelve la tabla de códigos actual del sistema.</returns>
        public Encoding GetEncoding()
        {
            //Contributed by Jan Stuchlík

            Encoding encoding = Encoding.UTF8;

            RtfTreeNode cpNode = RootNode.SelectSingleNode("ansicpg");

            if (cpNode != null)
            {
                encoding = Encoding.GetEncoding(cpNode.Parameter);
            }

            return encoding;
        }

        #endregion

        #region Métodos Privados

        /// <summary>
        /// Analiza el documento y lo carga con estructura de árbol.
        /// </summary>
        /// <returns>Se devuelve el valor 0 en caso de no producirse ningún error en la carga del documento.
        /// En caso contrario se devuelve el valor -1.</returns>
        private int parseRtfTree()
        {
            //Resultado de la carga del documento
            int res = 0;

            //Codificación por defecto del documento
            Encoding encoding = Encoding.UTF8;

            //Nodo actual
            RtfTreeNode curNode = rootNode;

            //Nuevos nodos para construir el árbol RTF
            RtfTreeNode newNode = null;

            //Se obtiene el primer token
            tok = lex.NextToken();

            while (tok.Type != RtfTokenType.Eof)
            {
                switch (tok.Type)
                {
                    case RtfTokenType.GroupStart:
                        newNode = new RtfTreeNode(RtfNodeType.Group,"GROUP",false,0);
                        curNode.AppendChild(newNode);
                        curNode = newNode;
                        level++;
                        break;
                    case RtfTokenType.GroupEnd:
                        curNode = curNode.ParentNode;
                        level--;
                        break;
                    case RtfTokenType.Keyword:
                    case RtfTokenType.Control:
                    case RtfTokenType.Text:
                        if (mergeSpecialCharacters)
                        {
                            //Contributed by Jan Stuchlík
                            bool isText = tok.Type == RtfTokenType.Text || (tok.Type == RtfTokenType.Control && tok.Key == "'");
                            if (curNode.LastChild != null && (curNode.LastChild.NodeType == RtfNodeType.Text && isText))
                            {
                                if (tok.Type == RtfTokenType.Text)
                                {
                                    curNode.LastChild.NodeKey += tok.Key;
                                    break;
                                }
                                if (tok.Type == RtfTokenType.Control && tok.Key == "'")
                                {
                                    curNode.LastChild.NodeKey += DecodeControlChar(tok.Parameter, encoding);
                                    break;
                                }
                            }
                            else
                            {
                                //Primer caracter especial \'
                                if (tok.Type == RtfTokenType.Control && tok.Key == "'")
                                {
                                    newNode = new RtfTreeNode(RtfNodeType.Text, DecodeControlChar(tok.Parameter, encoding), false, 0);
                                    curNode.AppendChild(newNode);
                                    break;
                                }
                            }
                        }

                        newNode = new RtfTreeNode(tok);
                        curNode.AppendChild(newNode);

                        if (mergeSpecialCharacters)
                        {
                            //Contributed by Jan Stuchlík
                            if (level == 1 && newNode.NodeType == RtfNodeType.Keyword && newNode.NodeKey == "ansicpg")
                            {
                                encoding = Encoding.GetEncoding(newNode.Parameter);
                            }
                        }

                        break;
                    default:
                        res = -1;
                        break;
                }

                //Se obtiene el siguiente token
                tok = lex.NextToken();
            }

            //Si el nivel actual no es 0 ( == Algun grupo no está bien formado )
            if (level != 0)
            {
                res = -1;
            }

            //Se devuelve el resultado de la carga
            return res;
        }

        /// <summary>
        /// Decodifica un caracter especial indicado por su código decimal
        /// </summary>
        /// <param name="code">Código del caracter especial (\')</param>
        /// <param name="enc">Codificación utilizada para decodificar el caracter especial.</param>
        /// <returns>Caracter especial decodificado.</returns>
        private string DecodeControlChar(int code, Encoding enc)
        {
            //Contributed by Jan Stuchlík
            return enc.GetString(new byte[] {(byte)code});                
        }

        /// <summary>
        /// Método auxiliar para generar la representación Textual del documento RTF.
        /// </summary>
        /// <param name="curNode">Nodo actual del árbol.</param>
        /// <param name="level">Nivel actual en árbol.</param>
        /// <param name="showNodeTypes">Indica si se mostrará el tipo de cada nodo del árbol.</param>
        /// <returns>Representación Textual del nodo 'curNode' con nivel 'level'</returns>
        private string toStringInm(RtfTreeNode curNode, int level, bool showNodeTypes)
        {
            StringBuilder res = new StringBuilder();

            RtfNodeCollection children = curNode.ChildNodes;

            for (int i = 0; i < level; i++)
                res.Append("  ");

            if (curNode.NodeType == RtfNodeType.Root)
                res.Append("ROOT\r\n");
            else if (curNode.NodeType == RtfNodeType.Group)
                res.Append("GROUP\r\n");
            else
            {
                if (showNodeTypes)
                {
                    res.Append(curNode.NodeType);
                    res.Append(": ");
                }

                res.Append(curNode.NodeKey);

                if (curNode.HasParameter)
                {
                    res.Append(" ");
                    res.Append(Convert.ToString(curNode.Parameter));
                }

                res.Append("\r\n");
            }

            if (children != null)
            {
                foreach (RtfTreeNode node in children)
                {
                    res.Append(toStringInm(node, level + 1, showNodeTypes));
                }
            }

            return res.ToString();
        }

		/// <summary>
		/// Parsea una fecha con formato "\yr2005\mo12\dy2\hr22\min56\sec15"
		/// </summary>
		/// <param name="group">Grupo RTF con la fecha.</param>
		/// <returns>Objeto DateTime con la fecha leida.</returns>
        private static DateTime parseDateTime(RtfTreeNode group)
        {
            DateTime dt;

            int year = 0, month = 0, day = 0, hour = 0, min = 0, sec = 0;

            foreach (RtfTreeNode node in group.ChildNodes)
            {
                switch (node.NodeKey)
                {
                    case "yr":
                        year = node.Parameter;
                        break;
                    case "mo":
                        month = node.Parameter;
                        break;
                    case "dy":
                        day = node.Parameter;
                        break;
                    case "hr":
                        hour = node.Parameter;
                        break;
                    case "min":
                        min = node.Parameter;
                        break;
                    case "sec":
                        sec = node.Parameter;
                        break;
                }
            }

            dt = new DateTime(year, month, day, hour, min, sec);

            return dt;
        }

        /// <summary>
        /// Extrae el texto de un árbol RTF.
        /// </summary>
        /// <returns>Texto plano del documento.</returns>
        private string ConvertToText()
        {
            StringBuilder res = new StringBuilder("");

            RtfTreeNode pardNode =
                MainGroup.SelectSingleChildNode("pard");

            for (int i = pardNode.Index; i < MainGroup.ChildNodes.Count; i++)
            {
                res.Append(MainGroup.ChildNodes[i].Text);
            }

            return res.ToString();
        }

        #endregion

        #region Propiedades

        /// <summary>
        /// Devuelve el nodo raíz del árbol del documento.
        /// </summary>
        public RtfTreeNode RootNode
        {
            get
            {
                //Se devuelve el nodo raíz del documento
                return rootNode;
            }
        }

        /// <summary>
        /// Devuelve el grupo principal del documento.
        /// </summary>
        public RtfTreeNode MainGroup
        {
            get
            { 
                //Se devuelve el grupo principal (null en caso de no existir)
                if (rootNode.HasChildNodes())
                    return rootNode.ChildNodes[0];
                else
                    return null;
            }
        }

        /// <summary>
        /// Devuelve el Texto RTF del documento.
        /// </summary>
        public string Rtf
        {
            get
            {
                //Se devuelve el Texto RTF del documento completo
                return rootNode.Rtf;
            }
        }

        /// <summary>
        /// Indica si se decodifican los caracteres especiales (\') uniéndolos a nodos de texto contiguos.
        /// </summary>
        public bool MergeSpecialCharacters
        {
            get
            {
                return mergeSpecialCharacters;
            }
            set
            {
                mergeSpecialCharacters = value;
            }
        }

        /// <summary>
        /// Devuelve el texto plano del documento.
        /// </summary>
        public string Text
        {
            get
            {
                return ConvertToText();
            }
        }

        #endregion
    }
}
