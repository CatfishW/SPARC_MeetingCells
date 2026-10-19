'use strict';
// Test-runner navigation over observed COLLIDER bounds. Only returns keyboard intent;
// it has no Unity handle, movement API, score access, or state mutation capability.
function routeTo(game, goal, width=1440, height=900) {
  const board=game.board;if(!board||!game.miniPlayer)return null;
  const rect=board.rect,cell=18;
  const radiusX=Math.max(3,(board.playerRadius?.x||0)*width*0.92),radiusY=Math.max(3,(board.playerRadius?.y||0)*height*0.92);
  const minX=rect.x*width+radiusX,minY=rect.y*height+radiusY,maxX=(rect.x+rect.w)*width-radiusX,maxY=(rect.y+rect.h)*height-radiusY;
  const cols=Math.max(2,Math.floor((maxX-minX)/cell)+1),rows=Math.max(2,Math.floor((maxY-minY)/cell)+1);
  const obstacles=(board.obstacles||[]).map(b=>({x:b.x*width,y:b.y*height,w:b.w*width,h:b.h*height,circle:b.circle}));
  const center=index=>({x:minX+(index%cols)*cell,y:minY+Math.floor(index/cols)*cell});
  function blockedPoint(p){return obstacles.some(b=>{if(b.circle){const rx=b.w/2+radiusX,ry=b.h/2+radiusY;return ((p.x-b.x-b.w/2)/rx)**2+((p.y-b.y-b.h/2)/ry)**2<1;}return p.x>b.x-radiusX&&p.x<b.x+b.w+radiusX&&p.y>b.y-radiusY&&p.y<b.y+b.h+radiusY;});}
  const blocked=Array.from({length:cols*rows},(_,i)=>blockedPoint(center(i)));
  const startPoint={x:game.miniPlayer.x*width,y:game.miniPlayer.y*height},target={x:goal.x*width,y:goal.y*height};
  let start=-1,startDistance=Infinity;
  for(let i=0;i<blocked.length;i++)if(!blocked[i]){const c=center(i),distance=Math.hypot(c.x-startPoint.x,c.y-startPoint.y);if(distance<startDistance){start=i;startDistance=distance;}}
  if(start<0)return null;
  const distance=point=>Math.hypot(point.x-target.x,point.y-target.y);
  const score=new Float64Array(cols*rows);score.fill(Infinity);score[start]=0;
  const previous=new Int32Array(cols*rows);previous.fill(-1);const open=new Set([start]),closed=new Set();let best=start,bestDistance=distance(center(start));
  while(open.size){
    let current=-1,priority=Infinity;
    for(const index of open){const value=score[index]+distance(center(index));if(value<priority){priority=value;current=index;}}
    open.delete(current);closed.add(current);const c=center(current),d=distance(c);if(d<bestDistance){best=current;bestDistance=d;}
    if(d<cell*0.8){best=current;break;}
    const x=current%cols,y=Math.floor(current/cols);
    for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
      if(!dx&&!dy)continue;const nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=cols||ny>=rows)continue;const next=ny*cols+nx;
      if(blocked[next]||closed.has(next))continue;
      if(dx&&dy&&(blocked[y*cols+nx]||blocked[ny*cols+x]))continue;
      const proposed=score[current]+Math.hypot(dx,dy)*cell;if(proposed<score[next]){score[next]=proposed;previous[next]=current;open.add(next);}
    }
  }
  const path=[];for(let p=best;p>=0;p=previous[p]){path.push(center(p));if(p===start)break;}path.reverse();
  if(!path.length)return null;
  let waypoint=path[Math.min(2,path.length-1)];
  if(path.length===1&&distance(startPoint)<Math.max(radiusX,radiusY)+25)waypoint=target;
  const dx=waypoint.x-startPoint.x,dy=waypoint.y-startPoint.y;
  const keys=[];if(Math.abs(dx)>7)keys.push(dx>0?'KeyD':'KeyA');if(Math.abs(dy)>7)keys.push(dy>0?'KeyS':'KeyW');
  return {keys,holdMilliseconds:Math.max(45,Math.min(150,Math.hypot(dx,dy)/Math.max(60,board.speed||200)*1000)),path,cost:score[best]+bestDistance,unreachableDistance:bestDistance,reachable:bestDistance<cell*0.8,targetId:goal.id};
}
function chooseMovement(game,width=1440,height=900){
  const goals=(game.tokens||[]).filter(t=>t.hostile),routes=[];let best=null;
  for(const target of goals){const route=routeTo(game,target,width,height);if(!route)continue;routes.push(route);
    // A nearby target across a solid wall must not starve a reachable hostile.
    // With no reachable target we retain the closest frontier and record the limitation.
    if(!best||(route.reachable&&!best.reachable)||(route.reachable===best.reachable&&route.cost<best.cost))best=route;
  }
  if(best)best.candidates=routes.map(route=>({targetId:route.targetId,reachable:route.reachable,cost:route.cost,unreachableDistance:route.unreachableDistance}));
  return best;
}
module.exports={routeTo,chooseMovement};
