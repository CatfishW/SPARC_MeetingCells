'use strict';
// Separate synthetic DOM contract: does not launch a browser or change the main JS test count.
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const crypto=require('node:crypto');
const root=path.resolve(__dirname,'..');
const htmlPath=path.join(root,'Assets/WebGLTemplates/SPARCOriginal/index.html');
const html=fs.readFileSync(htmlPath,'utf8');
const script=html.match(/<script id="sparc-native-keyboard-marker">([\s\S]*?)<\/script>/)?.[1];
const css=html.match(/<style id="sparc-native-keyboard-targets">([\s\S]*?)<\/style>/)?.[1];
assert(script&&css,'Scoped native keyboard adapter missing');
const checks=[];
const check=(name,fn)=>{fn();checks.push(name);};
const sha=value=>crypto.createHash('sha256').update(value).digest('hex');

class Element {
 constructor(){this.children=[];this.parentNode=null;this.attributes=new Map();this.attributeWrites=0;this.listeners=new Map();this.textContent='';this.style=Object.freeze({});}
 appendChild(node){node.parentNode=this;this.children.push(node);return node;}
 getAttribute(name){return this.attributes.get(name)??null;}
 setAttribute(name,value){assert.equal(name,'data-sparc-native-keyboard','Adapter must only write its owned marker');this.attributes.set(name,value);this.attributeWrites++;}
 addEventListener(name,fn){this.listeners.set(name,fn);}
}
class Div extends Element{}
class Input extends Element{}
class Textarea extends Element{}
class Button extends Element{}
const body=new Element();
const observers=[];
class Observer {
 constructor(callback){this.callback=callback;observers.push(this);}
 observe(target,options){this.target=target;this.options=options;}
}
const marker='data-sparc-native-keyboard';
function keyboard(multiline=false){
 const container=new Div(),input=multiline?new Textarea():new Input(),ok=new Button();
 Object.defineProperty(input,'value',{get:()=> 'RedDeliversO2',set:()=>assert.fail('Adapter must not change native input value')});
 input.maxLength=16;input.type='text';input.readOnly=false;input.disabled=false;
 input.style=Object.freeze(multiline?{height:'100px'}:{});
 input.onkeyup=()=>{};ok.ontouchend=()=>{};
 input.addEventListener('blur',()=>{});ok.addEventListener('touchend',()=>{});
 ok.textContent='OK';container.appendChild(input);container.appendChild(ok);
 container.input=input;container.okButton=ok;
 return {container,input,ok};
}
const existing=keyboard();body.appendChild(existing.container);
const original={input:existing.input,ok:existing.ok,onkeyup:existing.input.onkeyup,ontouchend:existing.ok.ontouchend,blur:existing.input.listeners.get('blur'),touchend:existing.ok.listeners.get('touchend')};
vm.runInNewContext(script,{document:{body},HTMLDivElement:Div,HTMLInputElement:Input,HTMLTextAreaElement:Textarea,HTMLButtonElement:Button,MutationObserver:Observer},{filename:htmlPath,timeout:1000});
const notify=node=>observers[0].callback([{addedNodes:[node]}]);

check('existing-native-keyboard-recognized-by-own-reference-identity',()=>assert.equal(existing.container.getAttribute(marker),'unity'));
check('observer-scoped-to-direct-body-child-additions',()=>{
 assert.equal(observers.length,1);assert.equal(observers[0].target,body);
 assert.deepEqual(Object.keys(observers[0].options),['childList']);assert.equal(observers[0].options.childList,true);
});
check('native-elements-values-constraints-and-handlers-preserved',()=>{
 assert.equal(existing.container.input,original.input);assert.equal(existing.container.okButton,original.ok);
 assert.equal(existing.input.value,'RedDeliversO2');assert.equal(existing.input.maxLength,16);assert.equal(existing.input.type,'text');
 assert.equal(existing.input.readOnly,false);assert.equal(existing.input.disabled,false);
 assert.equal(existing.input.onkeyup,original.onkeyup);assert.equal(existing.ok.ontouchend,original.ontouchend);
 assert.equal(existing.input.listeners.get('blur'),original.blur);assert.equal(existing.ok.listeners.get('touchend'),original.touchend);
 assert.equal(existing.input.listeners.size,1);assert.equal(existing.ok.listeners.size,1);
 assert.deepEqual(existing.container.children,[original.input,original.ok]);
});
check('marker-idempotent-without-handler-replacement',()=>{
 notify(existing.container);notify(existing.container);assert.equal(existing.container.attributeWrites,1);
 assert.equal(existing.ok.listeners.get('touchend'),original.touchend);
});
const multiline=keyboard(true);body.appendChild(multiline.container);notify(multiline.container);
check('native-textarea-recognized-with-original-100px-height-retained',()=>{
 assert.equal(multiline.container.getAttribute(marker),'unity');assert.equal(multiline.input.style.height,'100px');assert.equal(multiline.input.value,'RedDeliversO2');
});
check('ordinary-lookalike-input-and-OK-without-native-references-rejected',()=>{
 const k=keyboard();delete k.container.input;delete k.container.okButton;body.appendChild(k.container);notify(k.container);assert.equal(k.container.getAttribute(marker),null);
});
check('external-input-reference-rejected',()=>{
 const k=keyboard(),other=keyboard();k.container.input=other.input;body.appendChild(k.container);notify(k.container);assert.equal(k.container.getAttribute(marker),null);
});
check('external-button-reference-rejected',()=>{
 const k=keyboard(),other=keyboard();k.container.okButton=other.ok;body.appendChild(k.container);notify(k.container);assert.equal(k.container.getAttribute(marker),null);
});
check('nested-container-and-inherited-native-properties-rejected',()=>{
 const parent=new Div(),nested=keyboard();parent.appendChild(nested.container);body.appendChild(parent);notify(nested.container);assert.equal(nested.container.getAttribute(marker),null);
 const inherited=keyboard();const input=inherited.input;delete inherited.container.input;
 Object.setPrototypeOf(inherited.container,Object.assign(Object.create(Div.prototype),{input}));body.appendChild(inherited.container);notify(inherited.container);assert.equal(inherited.container.getAttribute(marker),null);
});
check('wrong-label-extra-children-and-wrong-native-element-types-rejected',()=>{
 const wrongLabel=keyboard();wrongLabel.ok.textContent='Save';
 const extra=keyboard();extra.container.appendChild(new Element());
 const wrongType=keyboard();wrongType.input=new Element();wrongType.container.children[0]=wrongType.input;wrongType.input.parentNode=wrongType.container;wrongType.container.input=wrongType.input;
 for(const k of [wrongLabel,extra,wrongType]){body.appendChild(k.container);notify(k.container);assert.equal(k.container.getAttribute(marker),null);}
});
check('unrelated-body-nodes-and-removed-keyboard-ignored',()=>{
 const unrelated=new Element();body.appendChild(unrelated);notify(unrelated);assert.equal(unrelated.getAttribute(marker),null);
 const removed=keyboard();notify(removed.container);assert.equal(removed.container.getAttribute(marker),null);
});
check('all-CSS-selectors-scoped-to-owned-native-marker',()=>{
 const clean=css.replace(/\/\*[\s\S]*?\*\//g,'');
 const rules=[...clean.matchAll(/([^{}]+)\{([^{}]*)\}/g)];assert.equal(rules.length,4);
 for(const rule of rules)for(const selector of rule[1].trim().split(','))assert(selector.trim().startsWith('[data-sparc-native-keyboard="unity"]'));
 for(const rule of rules.filter(rule=>rule[1].includes('>textarea')))assert(!/(?:^|;)\s*height\s*:/i.test(rule[2]),'Native textarea height must remain authored by Unity');
});
check('safe-area-border-box-grid-and-52px-targets-with-16px-input-font',()=>{
 const rules=[...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)];
 assert.match(rules[0][2],/box-sizing:border-box!important/);assert.match(rules[0][2],/grid-template-columns:minmax\(0,1fr\) 76px/);
 for(const side of ['left','right','bottom'])assert(rules[0][2].includes('env(safe-area-inset-'+side+')'));
 assert.match(rules[1][2],/min-height:52px!important/);assert.match(rules[1][2],/font:16px system-ui!important/);
 assert.match(rules[2][2],/height:52px!important/);assert.match(rules[2][2],/width:76px!important/);assert.match(rules[2][2],/font:16px system-ui!important/);
});
check('native-keyboard-paper-ink-teal-and-visible-focus-are-locally-scoped',()=>{
 const rules=[...css.matchAll(/([^{}]+)\{([^{}]*)\}/g)];
 assert.match(rules[0][2],/background:#fcf9ed!important/);assert.match(rules[0][2],/border-top:1px solid #294b4d!important/);
 assert.match(rules[1][2],/background:#f7f4e8!important/);assert.match(rules[1][2],/color:#203b3e!important/);
 assert.match(rules[2][2],/background:#356c6a!important/);assert.match(rules[2][2],/color:#fff!important/);
 assert(rules[3][1].includes(':focus-visible'));assert.match(rules[3][2],/outline:2px solid #203b3e!important/);
});
const output=path.resolve(process.argv[2]||path.join(root,'Reports/OriginalScene/native-mobile-keyboard-dom-contract.json'));
const report={status:'passed',createdAt:new Date().toISOString(),scope:'Synthetic DOM recognition and CSS contract only; actual CSS target geometry still requires genuine-touch browser acceptance.',inputHtml:path.relative(root,htmlPath),inputHtmlSha256:sha(html),markerScriptSha256:sha(script),fixtureSha256:sha(fs.readFileSync(__filename)),checks,checkCount:checks.length,mainJsTestCountUnchanged:true};
fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(output,JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({status:report.status,checkCount:checks.length,report:path.relative(root,output),inputHtmlSha256:report.inputHtmlSha256}));
