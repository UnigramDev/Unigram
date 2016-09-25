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
 * Class:		RtfNodeCollection
 * Description:	Colección de nodos de un árbol RTF.
 * ******************************************************************************/

using System.Collections;
using System.Collections.Generic;

namespace Unigram.Core.Rtf
{
    /// <summary>
    /// Colección de nodos de un documento RTF.
    /// </summary>
    public class RtfNodeCollection : List<RtfTreeNode>
    {
        #region Métodos Publicos

        /// <summary>
        /// Devuelve el índice del primer nodo de la colección cuya clave sea la pasada como parámetro.
        /// </summary>
        /// <param name="key">Clave a buscar en la colección.</param>
        /// <returns>Indice del nodo buscado. Devolverá el valor -1 en caso de no encontrarse el nodo dentro de la colección.</returns>
        public int IndexOf(string key)
        {
            int intFoundAt = -1;

            if (Count > 0)
            {
                for (int intIndex = 0; intIndex < Count; intIndex++)
                {
                    if (this[intIndex].NodeKey == key)
                    {
                        intFoundAt = intIndex;
                        break;
                    }
                }
            }

            return intFoundAt;
        }

        /// <summary>
        /// Devuelve el índice del primer nodo de la colección cuya clave sea la pasada como parámetro.
        /// </summary>
        /// <param name="key">Clave a buscar en la colección.</param>
        /// <param name="startIndex">Posición dentro de la colección a partir del que se buscará.</param>
        /// <returns>Indice del nodo buscado. Devolverá el valor -1 en caso de no encontrarse el nodo dentro de la colección.</returns>
        public int IndexOf(string key, int startIndex)
        {
            int intFoundAt = -1;

            if (Count > 0)
            {
                for (int intIndex = startIndex; intIndex < Count; intIndex++)
                {
                    if (this[intIndex].NodeKey == key)
                    {
                        intFoundAt = intIndex;
                        break;
                    }
                }
            }

            return intFoundAt;
        }

        /// <summary>
        /// Añade al final de la colección una nueva lista de nodos.
        /// </summary>
        /// <param name="collection">Nueva lista de nodos a añadir a la colección actual.</param>
        public void AddRange(RtfNodeCollection collection)
        {
            AddRange(collection);
        }

        #endregion
    }
}
