using System.Collections;
using System.Collections.Generic;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    //GameObjects
    public GameObject board;
    public GameObject[] cops = new GameObject[2];
    public GameObject robber;
    public Text rounds;
    public Text finalMessage;
    public Button playAgainButton;

    //Otras variables
    Tile[] tiles = new Tile[Constants.NumTiles];
    private int roundCount = 0;
    private int state;
    private int clickedTile = -1;
    private int clickedCop = 0;
                    
    void Start()
    {        
        InitTiles();
        InitAdjacencyLists();
        state = Constants.Init;
    }
        
    //Rellenamos el array de casillas y posicionamos las fichas
    void InitTiles()
    {
        for (int fil = 0; fil < Constants.TilesPerRow; fil++)
        {
            GameObject rowchild = board.transform.GetChild(fil).gameObject;            

            for (int col = 0; col < Constants.TilesPerRow; col++)
            {
                GameObject tilechild = rowchild.transform.GetChild(col).gameObject;                
                tiles[fil * Constants.TilesPerRow + col] = tilechild.GetComponent<Tile>();                         
            }
        }
                
        cops[0].GetComponent<CopMove>().currentTile=Constants.InitialCop0;
        cops[1].GetComponent<CopMove>().currentTile=Constants.InitialCop1;
        robber.GetComponent<RobberMove>().currentTile=Constants.InitialRobber;           
    }

    public void InitAdjacencyLists()
    {
        int[,] matriu = new int[Constants.NumTiles, Constants.NumTiles];

        for (int i = 0; i < Constants.NumTiles; i++)
        {
            int fila = i / 8;
            int columna = i % 8;

            if (fila > 0) matriu[i, i - 8] = 1;     // arriba
            if (fila < 7) matriu[i, i + 8] = 1;     // abajo
            if (columna > 0) matriu[i, i - 1] = 1;  // izquierda
            if (columna < 7) matriu[i, i + 1] = 1;  // derecha
        }

        for (int i = 0; i < Constants.NumTiles; i++)
        {
            tiles[i].adjacency = new List<int>();

            for (int j = 0; j < Constants.NumTiles; j++)
            {
                if (matriu[i, j] == 1)
                {
                    tiles[i].adjacency.Add(j);
                }
            }
        }
    }



    //Reseteamos cada casilla: color, padre, distancia y visitada
    public void ResetTiles()
    {        
        foreach (Tile tile in tiles)
        {
            tile.Reset();
        }
    }

    public void ClickOnCop(int cop_id)
    {
        switch (state)
        {
            case Constants.Init:
            case Constants.CopSelected:                
                clickedCop = cop_id;
                clickedTile = cops[cop_id].GetComponent<CopMove>().currentTile;
                tiles[clickedTile].current = true;

                ResetTiles();
                FindSelectableTiles(true);

                state = Constants.CopSelected;                
                break;            
        }
    }

    public void ClickOnTile(int t)
    {                     
        clickedTile = t;

        switch (state)
        {            
            case Constants.CopSelected:
                //Si es una casilla roja, nos movemos
                if (tiles[clickedTile].selectable) //cambio para buen funcionamiento
                {
                    // Verifica que no haya ya un policía en esa casilla
                    int otherCop = (clickedCop == 0) ? 1 : 0;
                    int otherCopTile = cops[otherCop].GetComponent<CopMove>().currentTile;

                    if (clickedTile == otherCopTile)
                    {
                        Debug.Log("No puedes mover dos policías a la misma casilla.");
                        return;
                    }

                    // Movimiento válido
                    cops[clickedCop].GetComponent<CopMove>().MoveToTile(tiles[clickedTile]);
                    cops[clickedCop].GetComponent<CopMove>().currentTile = tiles[clickedTile].numTile;
                    tiles[clickedTile].current = true;

                    state = Constants.TileSelected;
                }
                break;
            case Constants.TileSelected:
                state = Constants.Init;
                break;
            case Constants.RobberTurn:
                state = Constants.Init;
                break;
        }
    }

    public void FinishTurn()
    {
        switch (state)
        {            
            case Constants.TileSelected:
                ResetTiles();

                state = Constants.RobberTurn;
                RobberTurn();
                break;
            case Constants.RobberTurn:                
                ResetTiles();
                IncreaseRoundCount();
                if (roundCount <= Constants.MaxRounds)
                    state = Constants.Init;
                else
                    EndGame(false);
                break;
        }

    }

    public void RobberTurn()
    {
        clickedTile = robber.GetComponent<RobberMove>().currentTile;
        tiles[clickedTile].current = true;

        FindSelectableTiles(false);

        // Crear una lista con las casillas seleccionables
        List<Tile> opciones = new List<Tile>();
        for (int i = 0; i < Constants.NumTiles; i++)
        {
            if (tiles[i].selectable)
            {
                opciones.Add(tiles[i]);
            }
        }

        /* // Si hay alguna casilla alcanzable, movemos al ladrón
         if (opciones.Count > 0)
         {
             Tile destino = opciones[Random.Range(0, opciones.Count)];

             robber.GetComponent<RobberMove>().MoveToTile(destino);
             robber.GetComponent<RobberMove>().currentTile = destino.numTile;
         }
        */

        // EXTRA
        // Si no hay opciones, no hacemos nada
        if (opciones.Count == 0) return;

        // Posiciones de los policías
        int cop0 = cops[0].GetComponent<CopMove>().currentTile;
        int cop1 = cops[1].GetComponent<CopMove>().currentTile;

        // Distancias desde los policías
        Dictionary<int, int> distCop0 = CalcularDistanciasDesde(cop0);
        Dictionary<int, int> distCop1 = CalcularDistanciasDesde(cop1);

        Tile mejorOpcion = opciones[0];
        int mejorMinDistancia = -1;

        foreach (Tile t in opciones)
        {
            int d0 = distCop0.ContainsKey(t.numTile) ? distCop0[t.numTile] : 0;
            int d1 = distCop1.ContainsKey(t.numTile) ? distCop1[t.numTile] : 0;

            int minDistancia = Mathf.Min(d0, d1); // Importante: distancia al policía más cercano

            if (minDistancia > mejorMinDistancia)
            {
                mejorMinDistancia = minDistancia;
                mejorOpcion = t;
            }
        }

        // Mover al ladrón a la casilla más alejada del policía más cercano
        robber.GetComponent<RobberMove>().MoveToTile(mejorOpcion);
        robber.GetComponent<RobberMove>().currentTile = mejorOpcion.numTile;

    }
    


    public void EndGame(bool end)
    {
        if(end)
            finalMessage.text = "You Win!";
        else
            finalMessage.text = "You Lose!";
        playAgainButton.interactable = true;
        state = Constants.End;
    }

    public void PlayAgain()
    {
        cops[0].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop0]);
        cops[1].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop1]);
        robber.GetComponent<RobberMove>().Restart(tiles[Constants.InitialRobber]);
                
        ResetTiles();

        playAgainButton.interactable = false;
        finalMessage.text = "";
        roundCount = 0;
        rounds.text = "Rounds: ";

        state = Constants.Restarting;
    }

    public void InitGame()
    {
        state = Constants.Init;
         
    }

    public void IncreaseRoundCount()
    {
        roundCount++;
        rounds.text = "Rounds: " + roundCount;
    }

    public void FindSelectableTiles(bool cop)
    {
        int indexcurrentTile;

        if (cop)
            indexcurrentTile = cops[clickedCop].GetComponent<CopMove>().currentTile;
        else
            indexcurrentTile = robber.GetComponent<RobberMove>().currentTile;

        tiles[indexcurrentTile].current = true;

        Queue<Tile> nodes = new Queue<Tile>();
        Dictionary<Tile, int> distances = new Dictionary<Tile, int>();

        Tile startTile = tiles[indexcurrentTile];
        nodes.Enqueue(startTile);
        distances[startTile] = 0;

        while (nodes.Count > 0)
        {
            Tile current = nodes.Dequeue();
            int dist = distances[current];

            if (dist >= 2) continue;

            foreach (int neighborIndex in current.adjacency)
            {
                Tile neighbor = tiles[neighborIndex];

                if (distances.ContainsKey(neighbor)) continue;

                // Bloquear paso por la casilla del otro policía si estamos calculando para un policía
                if (cop)
                {
                    int otherCopIndex = (clickedCop == 0) ? 1 : 0;
                    int otherCopTile = cops[otherCopIndex].GetComponent<CopMove>().currentTile;
                    if (neighbor.numTile == otherCopTile) continue;
                }

                distances[neighbor] = dist + 1;
                nodes.Enqueue(neighbor);
            }
        }

        foreach (var pair in distances)
        {
            if (pair.Key.numTile != indexcurrentTile)
            {
                pair.Key.selectable = true;
            }
        }
    }

    // EXTRA
    // Calcula distancias desde una casilla origen usando BFS clásico
    private Dictionary<int, int> CalcularDistanciasDesde(int origen)
    {
        Dictionary<int, int> distancias = new Dictionary<int, int>();
        Queue<int> cola = new Queue<int>();

        distancias[origen] = 0;
        cola.Enqueue(origen);

        while (cola.Count > 0)
        {
            int actual = cola.Dequeue();
            int distancia = distancias[actual];

            foreach (int vecino in tiles[actual].adjacency)
            {
                if (!distancias.ContainsKey(vecino))
                {
                    distancias[vecino] = distancia + 1;
                    cola.Enqueue(vecino);
                }
            }
        }

        return distancias;
    }



}
